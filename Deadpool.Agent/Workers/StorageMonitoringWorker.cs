using Deadpool.Agent.Configuration;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deadpool.Agent.Workers;

public class StorageMonitoringWorker : BackgroundService
{
    private readonly ILogger<StorageMonitoringWorker> _logger;
    private readonly IStorageMonitoringService _storageMonitoringService;
    private readonly IStorageHealthCheckRepository _healthCheckRepository;
    private readonly StorageMonitoringOptions _options;
    private readonly Dictionary<string, HealthStatus> _lastKnownStatus = new();
    private int _checkCounter = 0;

    public StorageMonitoringWorker(
        ILogger<StorageMonitoringWorker> logger,
        IStorageMonitoringService storageMonitoringService,
        IStorageHealthCheckRepository healthCheckRepository,
        IOptions<StorageMonitoringOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageMonitoringService = storageMonitoringService ?? throw new ArgumentNullException(nameof(storageMonitoringService));
        _healthCheckRepository = healthCheckRepository ?? throw new ArgumentNullException(nameof(healthCheckRepository));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Storage Monitoring Worker starting. Check interval: {Interval}", _options.CheckInterval);

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformStorageChecksAsync(stoppingToken);

                _checkCounter++;
                if (_checkCounter % 10 == 0)
                {
                    PerformRetentionCleanup();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in storage monitoring loop");
            }

            try
            {
                await Task.Delay(_options.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Storage Monitoring Worker stopping");
    }

    private async Task PerformStorageChecksAsync(CancellationToken cancellationToken)
    {
        if (_options.MonitoredVolumes == null || _options.MonitoredVolumes.Count == 0)
        {
            _logger.LogWarning("No storage volumes configured for monitoring");
            return;
        }

        foreach (var volumePath in _options.MonitoredVolumes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await CheckVolumeHealthAsync(volumePath, cancellationToken);
        }
    }

    private async Task CheckVolumeHealthAsync(string volumePath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Checking storage health for volume: {VolumePath}", volumePath);

            var healthCheck = await _storageMonitoringService.CheckStorageHealthAsync(volumePath);

            await _healthCheckRepository.CreateAsync(healthCheck);

            LogHealthTransition(volumePath, healthCheck.OverallHealth);

            _lastKnownStatus[volumePath] = healthCheck.OverallHealth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check storage health for volume: {VolumePath}", volumePath);
        }
    }

    private void LogHealthTransition(string volumePath, HealthStatus newStatus)
    {
        if (!_lastKnownStatus.TryGetValue(volumePath, out var lastStatus) || lastStatus != newStatus)
        {
            switch (newStatus)
            {
                case HealthStatus.Healthy:
                    _logger.LogInformation(
                        "Storage health RECOVERED for {VolumePath}: Now {Status}",
                        volumePath, newStatus);
                    break;
                case HealthStatus.Warning:
                    _logger.LogWarning(
                        "Storage health DEGRADED for {VolumePath}: Now {Status}",
                        volumePath, newStatus);
                    break;
                case HealthStatus.Critical:
                    _logger.LogError(
                        "Storage health CRITICAL for {VolumePath}: Now {Status}",
                        volumePath, newStatus);
                    break;
            }
        }
    }

    private void PerformRetentionCleanup()
    {
        try
        {
            var retentionPeriod = TimeSpan.FromDays(_options.HealthCheckRetentionDays);
            _healthCheckRepository.CleanupOldHealthChecks(retentionPeriod);

            _logger.LogDebug("Storage health check retention cleanup completed. Retention period: {Period} days",
                retentionPeriod.TotalDays);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to perform storage health check retention cleanup");
        }
    }
}
