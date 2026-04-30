using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Core.Services;

/// <summary>
/// Aggregates monitoring data for operational dashboard.
/// Reads pre-computed health checks written by the Agent — UI is read-only.
/// </summary>
public class DashboardMonitoringService : IDashboardMonitoringService
{
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IBackupHealthCheckRepository _backupHealthCheckRepository;
    private readonly IStorageHealthCheckRepository _storageHealthCheckRepository;
    private readonly IDatabasePulseRepository _databasePulseRepository;
    private readonly ILogger<DashboardMonitoringService> _logger;
    private const int RecentJobCount = 20;

    public DashboardMonitoringService(
        IBackupJobRepository backupJobRepository,
        IBackupHealthCheckRepository backupHealthCheckRepository,
        IStorageHealthCheckRepository storageHealthCheckRepository,
        IDatabasePulseRepository databasePulseRepository,
        ILogger<DashboardMonitoringService> logger)
    {
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
        _backupHealthCheckRepository = backupHealthCheckRepository ?? throw new ArgumentNullException(nameof(backupHealthCheckRepository));
        _storageHealthCheckRepository = storageHealthCheckRepository ?? throw new ArgumentNullException(nameof(storageHealthCheckRepository));
        _databasePulseRepository = databasePulseRepository ?? throw new ArgumentNullException(nameof(databasePulseRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DashboardSnapshot> GetDashboardSnapshotAsync(string databaseName, string backupVolumePath)
    {
        var lastBackupStatus = await BuildLastBackupStatusAsync(databaseName);
        var chainInitializationStatus = await BuildChainInitializationStatusAsync(databaseName);
        var recentJobs = await BuildRecentJobsAsync(databaseName);
        var storageStatus = await BuildStorageStatusAsync(backupVolumePath);
        var pulseStatus = await BuildDatabasePulseStatusAsync();

        return new DashboardSnapshot(
            databaseName,
            lastBackupStatus,
            chainInitializationStatus,
            recentJobs,
            storageStatus,
            pulseStatus);
    }

    private async Task<ChainInitializationStatusSummary> BuildChainInitializationStatusAsync(string databaseName)
    {
        try
        {
            var lastSuccessfulFull = await _backupJobRepository.GetLastSuccessfulFullBackupAsync(databaseName);
            var isInitialized = lastSuccessfulFull != null;

            var restoreChainHealth = isInitialized ? "Healthy" : "Unhealthy";

            return new ChainInitializationStatusSummary
            {
                IsInitialized = isInitialized,
                LastValidFullBackupTime = lastSuccessfulFull?.EndTime,
                LastValidFullBackupPath = lastSuccessfulFull?.BackupFilePath,
                RestoreChainHealth = restoreChainHealth,
                WarningMessage = isInitialized
                    ? string.Empty
                    : "System not yet protected — Full backup has not been completed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build chain initialization status for database '{DatabaseName}'.", databaseName);
            return new ChainInitializationStatusSummary
            {
                IsInitialized = null,
                LastValidFullBackupTime = null,
                LastValidFullBackupPath = null,
                RestoreChainHealth = "Unknown",
                WarningMessage = "Backup status unknown — verify system"
            };
        }
    }

    private async Task<LastBackupStatus> BuildLastBackupStatusAsync(string databaseName)
    {
        try
        {
            var healthCheck = await _backupHealthCheckRepository.GetLatestHealthCheckAsync(databaseName);

            if (healthCheck == null)
            {
                return new LastBackupStatus
                {
                    OverallHealth = HealthStatus.Warning,
                    Warnings = new List<string> { "No backup health data available — is the Agent running?" },
                    ChainHealthSummary = "No data"
                };
            }

            var chainSummary = healthCheck.OverallHealth switch
            {
                HealthStatus.Healthy => "Healthy",
                HealthStatus.Warning => "Warning",
                HealthStatus.Critical => "Critical",
                _ => "Unknown"
            };

            return new LastBackupStatus
            {
                LastFullBackup = healthCheck.LastSuccessfulFullBackup,
                LastDifferentialBackup = healthCheck.LastSuccessfulDifferentialBackup,
                LastLogBackup = healthCheck.LastSuccessfulLogBackup,
                OverallHealth = healthCheck.OverallHealth,
                Warnings = healthCheck.Warnings.ToList(),
                CriticalIssues = healthCheck.CriticalFindings.ToList(),
                ChainHealthSummary = chainSummary
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build last backup status for database '{DatabaseName}'.", databaseName);
            return new LastBackupStatus
            {
                OverallHealth = HealthStatus.Warning,
                Warnings = new List<string> { "Failed to read backup health data" },
                ChainHealthSummary = "Unknown"
            };
        }
    }

    private async Task<List<RecentJobSummary>> BuildRecentJobsAsync(string databaseName)
    {
        var recentJobs = await _backupJobRepository.GetRecentJobsAsync(databaseName, RecentJobCount);

        return recentJobs
            .Select(job => new RecentJobSummary(
                job.BackupType,
                job.StartTime,
                job.EndTime,
                job.Status,
                job.BackupFilePath,
                job.ErrorMessage))
            .ToList();
    }

    private async Task<StorageStatusSummary> BuildStorageStatusAsync(string backupVolumePath)
    {
        try
        {
            var healthCheck = await _storageHealthCheckRepository.GetLatestHealthCheckAsync(backupVolumePath);

            if (healthCheck == null)
            {
                return new StorageStatusSummary(
                    backupVolumePath, 0L, 0L, 0m,
                    HealthStatus.Warning,
                    new List<string> { "No storage health data available — is the Agent running?" },
                    new List<string>());
            }

            return new StorageStatusSummary(
                healthCheck.VolumePath,
                healthCheck.TotalBytes,
                healthCheck.FreeBytes,
                healthCheck.FreePercentage,
                healthCheck.OverallHealth,
                healthCheck.Warnings.ToList(),
                healthCheck.CriticalFindings.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build storage status for volume '{VolumePath}'.", backupVolumePath);
            return new StorageStatusSummary(
                backupVolumePath, 0L, 0L, 0m,
                HealthStatus.Warning,
                new List<string> { "Failed to read storage health data" },
                new List<string>());
        }
    }

    private async Task<DatabasePulseStatus?> BuildDatabasePulseStatusAsync()
    {
        try
        {
            var record = await _databasePulseRepository.GetLatestAsync();
            if (record == null) return null;

            return new DatabasePulseStatus(record.Status, record.CheckTime, record.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read database pulse status.");
            return null;
        }
    }
}
