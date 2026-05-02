using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

public interface IBackupHealthMonitoringService
{
    Task<BackupHealthCheck> CheckDatabaseHealthAsync(string databaseName, BackupPolicy policy);
}

public class BackupHealthMonitoringService : IBackupHealthMonitoringService
{
    private readonly IBackupJobRepository _repository;
    private readonly BackupHealthOptions _options;

    public BackupHealthMonitoringService(
        IBackupJobRepository repository,
        BackupHealthOptions options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<BackupHealthCheck> CheckDatabaseHealthAsync(string databaseName, BackupPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (policy == null)
            throw new ArgumentNullException(nameof(policy));

        var healthCheck = new BackupHealthCheck(databaseName);

        AddMonitoringLimitations(healthCheck);

        await CheckLastBackupStatusAsync(healthCheck, policy);
        await CheckOverdueBackupsAsync(healthCheck, policy);
        await CheckBackupChainHealthAsync(healthCheck, policy);
        await CheckBackupFileExistenceAsync(healthCheck);
        CheckRecentBackupFailures(healthCheck);

        return healthCheck;
    }

    private void AddMonitoringLimitations(BackupHealthCheck healthCheck)
    {
        healthCheck.AddLimitation("LSN sequence validation not implemented - log chain continuity is time-based only");
        healthCheck.AddLimitation("Differential base validation not implemented - assumes differential matches current full backup");
        healthCheck.AddLimitation("Backup file integrity (CHECKSUM) not validated - only existence is checked");
    }

    private async Task CheckLastBackupStatusAsync(BackupHealthCheck healthCheck, BackupPolicy policy)
    {
        var lastFullBackup = await _repository.GetLastSuccessfulBackupAsync(
            healthCheck.DatabaseName, BackupType.Full);

        if (lastFullBackup != null)
        {
            var completionTime = lastFullBackup.EndTime ?? lastFullBackup.StartTime;
            healthCheck.RecordLastSuccessfulFullBackup(completionTime);
        }

        var lastDifferentialBackup = await _repository.GetLastSuccessfulBackupAsync(
            healthCheck.DatabaseName, BackupType.Differential);

        if (lastDifferentialBackup != null)
        {
            var completionTime = lastDifferentialBackup.EndTime ?? lastDifferentialBackup.StartTime;
            healthCheck.RecordLastSuccessfulDifferentialBackup(completionTime);
        }

        if (policy.SupportsTransactionLogBackup())
        {
            var lastLogBackup = await _repository.GetLastSuccessfulBackupAsync(
                healthCheck.DatabaseName, BackupType.TransactionLog);

            if (lastLogBackup != null)
            {
                var completionTime = lastLogBackup.EndTime ?? lastLogBackup.StartTime;
                healthCheck.RecordLastSuccessfulLogBackup(completionTime);
            }
        }

        var lastFailedBackup = await _repository.GetLastFailedBackupAsync(healthCheck.DatabaseName);

        if (lastFailedBackup != null)
        {
            var failureTime = lastFailedBackup.EndTime ?? lastFailedBackup.StartTime;
            healthCheck.RecordLastFailedBackup(failureTime);
        }
    }

    private async Task CheckOverdueBackupsAsync(BackupHealthCheck healthCheck, BackupPolicy policy)
    {
        var now = DateTime.Now;

        var lastFullBackup = await _repository.GetLastSuccessfulBackupAsync(
            healthCheck.DatabaseName, BackupType.Full);

        if (lastFullBackup == null)
        {
            healthCheck.AddCriticalFinding("No successful full backup found.");
        }
        else
        {
            var completionTime = lastFullBackup.EndTime ?? lastFullBackup.StartTime;
            var fullBackupAge = now - completionTime;

            if (fullBackupAge > _options.FullBackupOverdueThreshold)
            {
                healthCheck.AddWarning(
                    $"Full backup overdue. Last completed: {completionTime:yyyy-MM-dd HH:mm:ss} ({fullBackupAge.TotalHours:F1}h ago).");
            }
        }

        var lastDifferentialBackup = await _repository.GetLastSuccessfulBackupAsync(
            healthCheck.DatabaseName, BackupType.Differential);

        if (lastDifferentialBackup != null)
        {
            var completionTime = lastDifferentialBackup.EndTime ?? lastDifferentialBackup.StartTime;
            var differentialBackupAge = now - completionTime;

            if (differentialBackupAge > _options.DifferentialBackupOverdueThreshold)
            {
                healthCheck.AddWarning(
                    $"Differential backup overdue. Last completed: {completionTime:yyyy-MM-dd HH:mm:ss} ({differentialBackupAge.TotalHours:F1}h ago).");
            }
        }

        if (policy.SupportsTransactionLogBackup())
        {
            var lastLogBackup = await _repository.GetLastSuccessfulBackupAsync(
                healthCheck.DatabaseName, BackupType.TransactionLog);

            if (lastLogBackup == null)
            {
                healthCheck.AddWarning("No successful transaction log backup found.");
            }
            else
            {
                var completionTime = lastLogBackup.EndTime ?? lastLogBackup.StartTime;
                var logBackupAge = now - completionTime;

                if (logBackupAge > _options.LogBackupOverdueThreshold)
                {
                    healthCheck.AddWarning(
                        $"Transaction log backup overdue. Last completed: {completionTime:yyyy-MM-dd HH:mm:ss} ({logBackupAge.TotalMinutes:F1}m ago).");
                }
            }
        }
    }

    private async Task CheckBackupChainHealthAsync(BackupHealthCheck healthCheck, BackupPolicy policy)
    {
        var since = DateTime.Now - _options.ChainLookbackPeriod;
        var chain = await _repository.GetBackupChainAsync(healthCheck.DatabaseName, since);

        var chainList = chain.ToList();

        var hasFullBackup = chainList.Any(j => j.BackupType == BackupType.Full);

        if (!hasFullBackup)
        {
            healthCheck.AddCriticalFinding(
                $"No full backup in chain (lookback: {_options.ChainLookbackPeriod.TotalDays:F0} days). Restore impossible.");
            return;
        }

        var lastFullBackup = chainList
            .Where(j => j.BackupType == BackupType.Full)
            .OrderByDescending(j => j.StartTime)
            .First();

        var differentialBackups = chainList
            .Where(j => j.BackupType == BackupType.Differential)
            .Where(j => j.StartTime > lastFullBackup.StartTime)
            .OrderBy(j => j.StartTime)
            .ToList();

        var logBackups = chainList
            .Where(j => j.BackupType == BackupType.TransactionLog)
            .Where(j => j.StartTime > lastFullBackup.StartTime)
            .OrderBy(j => j.StartTime)
            .ToList();

        if (differentialBackups.Any())
        {
            var lastDifferential = differentialBackups.Last();
            var logsAfterDifferential = logBackups
                .Where(j => j.StartTime > lastDifferential.StartTime)
                .ToList();

            if (policy.SupportsTransactionLogBackup() && !logsAfterDifferential.Any())
            {
                healthCheck.AddWarning(
                    "No log backups after last differential. Point-in-time recovery may be limited.");
            }
        }

        if (policy.SupportsTransactionLogBackup() && logBackups.Any())
        {
            var expectedGap = _options.LogBackupOverdueThreshold;

            for (int i = 1; i < logBackups.Count; i++)
            {
                var previousLog = logBackups[i - 1];
                var currentLog = logBackups[i];
                var gap = currentLog.StartTime - previousLog.StartTime;

                if (gap > expectedGap * 3)
                {
                    healthCheck.AddWarning(
                        $"Large gap in log backup chain: {gap.TotalMinutes:F1}m between backups.");
                }
            }
        }
    }

    private async Task CheckBackupFileExistenceAsync(BackupHealthCheck healthCheck)
    {
        var lastFullBackup = await _repository.GetLastSuccessfulBackupAsync(
            healthCheck.DatabaseName, BackupType.Full);

        if (lastFullBackup != null && !string.IsNullOrWhiteSpace(lastFullBackup.BackupFilePath))
        {
            var path = lastFullBackup.BackupFilePath;

            // Skip placeholder paths
            if (path.Contains("PENDING"))
                return;

            // Check all rooted paths (Windows, UNC, Linux/POSIX)
            if (Path.IsPathRooted(path) && !File.Exists(path))
            {
                healthCheck.AddCriticalFinding(
                    $"Full backup file missing: {path}. Restore impossible.");
            }
        }
    }

    private void CheckRecentBackupFailures(BackupHealthCheck healthCheck)
    {
        if (healthCheck.LastFailedBackup.HasValue)
        {
            var failureAge = DateTime.Now - healthCheck.LastFailedBackup.Value;

            if (failureAge < TimeSpan.FromHours(24))
            {
                healthCheck.AddWarning(
                    $"Recent backup failure detected: {failureAge.TotalHours:F1}h ago at {healthCheck.LastFailedBackup.Value:yyyy-MM-dd HH:mm:ss}");
            }
        }
    }
}
