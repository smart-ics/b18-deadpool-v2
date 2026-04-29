using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

/// <summary>
/// Aggregates monitoring data for operational dashboard.
/// </summary>
public class DashboardMonitoringService : IDashboardMonitoringService
{
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IStorageMonitoringService _storageMonitoringService;
    private const int RecentJobCount = 20;

    public DashboardMonitoringService(
        IBackupJobRepository backupJobRepository,
        IStorageMonitoringService storageMonitoringService)
    {
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
        _storageMonitoringService = storageMonitoringService ?? throw new ArgumentNullException(nameof(storageMonitoringService));
    }

    public async Task<DashboardSnapshot> GetDashboardSnapshotAsync(string databaseName, string backupVolumePath)
    {
        var lastBackupStatus = await BuildLastBackupStatusAsync(databaseName);
        var recentJobs = await BuildRecentJobsAsync(databaseName);
        var storageStatus = await BuildStorageStatusAsync(backupVolumePath);

        return new DashboardSnapshot(
            databaseName,
            lastBackupStatus,
            recentJobs,
            storageStatus);
    }

    private async Task<LastBackupStatus> BuildLastBackupStatusAsync(string databaseName)
    {
        var lastFull = await _backupJobRepository.GetLastSuccessfulBackupAsync(databaseName, BackupType.Full);
        var lastDiff = await _backupJobRepository.GetLastSuccessfulBackupAsync(databaseName, BackupType.Differential);
        var lastLog = await _backupJobRepository.GetLastSuccessfulBackupAsync(databaseName, BackupType.TransactionLog);
        var lastFailed = await _backupJobRepository.GetLastFailedBackupAsync(databaseName);

        var warnings = new List<string>();
        var criticalIssues = new List<string>();
        var overallHealth = HealthStatus.Healthy;
        var chainHealthSummary = "Unknown";

        // Check Full backup
        if (lastFull == null)
        {
            criticalIssues.Add("No Full backup found");
            overallHealth = HealthStatus.Critical;
            chainHealthSummary = "Broken - No Full backup";
        }
        else
        {
            var fullAge = DateTime.UtcNow - lastFull.EndTime!.Value;
            if (fullAge.TotalHours > 72)
            {
                criticalIssues.Add($"Full backup is {fullAge.TotalHours:F0} hours old (>72h)");
                overallHealth = HealthStatus.Critical;
                chainHealthSummary = "Critical - Full backup overdue";
            }
            else if (fullAge.TotalHours > 48)
            {
                warnings.Add($"Full backup is {fullAge.TotalHours:F0} hours old (>48h)");
                if (overallHealth == HealthStatus.Healthy)
                    overallHealth = HealthStatus.Warning;
                chainHealthSummary = "Warning - Full backup aging";
            }
            else
            {
                chainHealthSummary = "Healthy";
            }
        }

        // Check Log backup
        if (lastLog != null)
        {
            var logAge = DateTime.UtcNow - lastLog.EndTime!.Value;
            if (logAge.TotalMinutes > 60)
            {
                warnings.Add($"Log backup is {logAge.TotalMinutes:F0} minutes old (>60m)");
                if (overallHealth == HealthStatus.Healthy)
                    overallHealth = HealthStatus.Warning;
            }
        }

        // Check recent failures
        if (lastFailed != null)
        {
            var failureAge = DateTime.UtcNow - lastFailed.StartTime;
            if (failureAge.TotalHours < 24)
            {
                warnings.Add($"Recent failure: {lastFailed.ErrorMessage ?? "Unknown error"}");
                if (overallHealth == HealthStatus.Healthy)
                    overallHealth = HealthStatus.Warning;
            }
        }

        return new LastBackupStatus
        {
            LastFullBackup = lastFull?.EndTime,
            LastDifferentialBackup = lastDiff?.EndTime,
            LastLogBackup = lastLog?.EndTime,
            OverallHealth = overallHealth,
            Warnings = warnings,
            CriticalIssues = criticalIssues,
            ChainHealthSummary = chainHealthSummary
        };
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
        var storageHealth = await _storageMonitoringService.CheckStorageHealthAsync(backupVolumePath);

        return new StorageStatusSummary(
            storageHealth.VolumePath,
            storageHealth.TotalBytes,
            storageHealth.FreeBytes,
            storageHealth.FreePercentage,
            storageHealth.OverallHealth,
            storageHealth.Warnings.ToList(),
            storageHealth.CriticalFindings.ToList());
    }
}
