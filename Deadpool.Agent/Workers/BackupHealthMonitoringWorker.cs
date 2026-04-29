using Deadpool.Agent.Configuration;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Microsoft.Extensions.Options;

namespace Deadpool.Agent.Workers;

public sealed class BackupHealthMonitoringWorker : BackgroundService
{
    private readonly ILogger<BackupHealthMonitoringWorker> _logger;
    private readonly IBackupHealthMonitoringService _healthMonitoringService;
    private readonly IBackupHealthCheckRepository _healthCheckRepository;
    private readonly IOptions<List<DatabaseBackupPolicyOptions>> _policyOptions;
    private readonly HealthMonitoringOptions _monitoringOptions;
    private readonly Dictionary<string, Core.Domain.Enums.HealthStatus> _lastKnownStatus = new();
    private int _checkCounter = 0;

    public BackupHealthMonitoringWorker(
        ILogger<BackupHealthMonitoringWorker> logger,
        IBackupHealthMonitoringService healthMonitoringService,
        IBackupHealthCheckRepository healthCheckRepository,
        IOptions<List<DatabaseBackupPolicyOptions>> policyOptions,
        IOptions<HealthMonitoringOptions> monitoringOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthMonitoringService = healthMonitoringService ?? throw new ArgumentNullException(nameof(healthMonitoringService));
        _healthCheckRepository = healthCheckRepository ?? throw new ArgumentNullException(nameof(healthCheckRepository));
        _policyOptions = policyOptions ?? throw new ArgumentNullException(nameof(policyOptions));
        _monitoringOptions = monitoringOptions?.Value ?? throw new ArgumentNullException(nameof(monitoringOptions));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackupHealthMonitoringWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthChecksAsync(stoppingToken);

                _checkCounter++;

                if (_checkCounter % 10 == 0)
                {
                    PerformRetentionCleanup();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in health monitoring worker loop.");
            }

            await Task.Delay(_monitoringOptions.CheckInterval, stoppingToken);
        }

        _logger.LogInformation("BackupHealthMonitoringWorker stopped.");
    }

    private void PerformRetentionCleanup()
    {
        try
        {
            var retentionPeriod = TimeSpan.FromDays(_monitoringOptions.HealthCheckRetentionDays);
            _healthCheckRepository.CleanupOldHealthChecks(retentionPeriod);

            _logger.LogDebug("Health check retention cleanup completed. Retention period: {Period} days", 
                retentionPeriod.TotalDays);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to perform health check retention cleanup");
        }
    }

    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        var policies = _policyOptions.Value;

        foreach (var policyOptions in policies)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await CheckDatabaseHealthAsync(policyOptions, cancellationToken);
        }
    }

    private async Task CheckDatabaseHealthAsync(
        DatabaseBackupPolicyOptions policyOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var policy = ConvertToBackupPolicy(policyOptions);

            var healthCheck = await _healthMonitoringService.CheckDatabaseHealthAsync(
                policy.DatabaseName,
                policy);

            await _healthCheckRepository.CreateAsync(healthCheck);

            var previousStatus = _lastKnownStatus.GetValueOrDefault(healthCheck.DatabaseName, Core.Domain.Enums.HealthStatus.Healthy);
            var currentStatus = healthCheck.OverallHealth;
            var statusChanged = previousStatus != currentStatus;

            if (statusChanged)
            {
                _lastKnownStatus[healthCheck.DatabaseName] = currentStatus;

                if (healthCheck.IsCritical())
                {
                    _logger.LogCritical(
                        "CRITICAL backup health transition for {Database} (was {Previous}): {Findings}",
                        healthCheck.DatabaseName,
                        previousStatus,
                        string.Join("; ", healthCheck.CriticalFindings));
                }
                else if (healthCheck.HasWarnings())
                {
                    _logger.LogWarning(
                        "Backup health transition to Warning for {Database} (was {Previous}): {Warnings}",
                        healthCheck.DatabaseName,
                        previousStatus,
                        string.Join("; ", healthCheck.Warnings));
                }
                else
                {
                    _logger.LogInformation(
                        "Backup health recovered to Healthy for {Database} (was {Previous})",
                        healthCheck.DatabaseName,
                        previousStatus);
                }
            }
            else
            {
                if (healthCheck.IsCritical())
                {
                    _logger.LogDebug(
                        "Backup health remains Critical for {Database}",
                        healthCheck.DatabaseName);
                }
                else if (healthCheck.HasWarnings())
                {
                    _logger.LogDebug(
                        "Backup health remains Warning for {Database}",
                        healthCheck.DatabaseName);
                }
            }

            if (healthCheck.Limitations.Any())
            {
                _logger.LogDebug(
                    "Monitoring limitations for {Database}: {Limitations}",
                    healthCheck.DatabaseName,
                    string.Join("; ", healthCheck.Limitations));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check health for database {Database}",
                policyOptions.DatabaseName);
        }
    }

    private static BackupPolicy ConvertToBackupPolicy(DatabaseBackupPolicyOptions options)
    {
        var recoveryModel = Enum.Parse<Core.Domain.Enums.RecoveryModel>(options.RecoveryModel, ignoreCase: true);

        BackupSchedule? logSchedule = null;
        if (!string.IsNullOrWhiteSpace(options.TransactionLogBackupCron))
            logSchedule = new BackupSchedule(options.TransactionLogBackupCron);

        return new BackupPolicy(
            databaseName: options.DatabaseName,
            recoveryModel: recoveryModel,
            fullBackupSchedule: new BackupSchedule(options.FullBackupCron),
            differentialBackupSchedule: new BackupSchedule(options.DifferentialBackupCron),
            transactionLogBackupSchedule: logSchedule,
            retentionDays: options.RetentionDays
        );
    }
}
