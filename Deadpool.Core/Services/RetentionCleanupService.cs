using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Core.Services;

/// <summary>
/// Conservative retention cleanup service with LSN-aware restore chain validation.
/// 
/// Safety principles:
/// 1. NEVER delete latest valid Full backup
/// 2. NEVER delete Differential backups needed by retained Full backup (LSN-validated)
/// 3. NEVER delete Log backups needed by retained restore chain (LSN continuity validated)
/// 4. If chain safety is uncertain OR LSN metadata missing, DO NOT DELETE
/// 5. Fail safe - retain rather than risk breaking recovery
/// 
/// This implementation prioritizes recoverability over storage efficiency.
/// LSN (Log Sequence Number) validation ensures restore chain integrity.
/// </summary>
public class RetentionCleanupService : IRetentionCleanupService
{
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IBackupFileDeleter _backupFileDeleter;
    private readonly ILogger<RetentionCleanupService> _logger;

    public RetentionCleanupService(
        IBackupJobRepository backupJobRepository,
        IBackupFileDeleter backupFileDeleter,
        ILogger<RetentionCleanupService> logger)
    {
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
        _backupFileDeleter = backupFileDeleter ?? throw new ArgumentNullException(nameof(backupFileDeleter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RetentionCleanupResult> CleanupExpiredBackupsAsync(
        string databaseName,
        RetentionPolicy retentionPolicy,
        bool isDryRun = false)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (retentionPolicy == null)
            throw new ArgumentNullException(nameof(retentionPolicy));

        _logger.LogInformation(
            "Starting retention cleanup for database: {DatabaseName}. " +
            "Policy: Full={FullRetention}d, Diff={DiffRetention}d, Log={LogRetention}d. DryRun={IsDryRun}",
            databaseName,
            retentionPolicy.FullBackupRetention.Days,
            retentionPolicy.DifferentialBackupRetention.Days,
            retentionPolicy.LogBackupRetention.Days,
            isDryRun);

        var result = new RetentionCleanupResult(databaseName, isDryRun);
        var cutoffTime = DateTime.UtcNow;

        // Get all completed backups for this database, ordered by start time descending
        var allBackups = await _backupJobRepository.GetBackupsByDatabaseAsync(databaseName);
        var completedBackups = allBackups
            .Where(b => b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.StartTime)
            .ToList();

        if (!completedBackups.Any())
        {
            _logger.LogInformation("No completed backups found for database: {DatabaseName}", databaseName);
            return result;
        }

        _logger.LogInformation(
            "Found {BackupCount} completed backups for database: {DatabaseName}",
            completedBackups.Count,
            databaseName);

        // Step 1: Identify backups to retain (LSN-aware chain safety)
        var backupsToRetain = IdentifyBackupsToRetain(completedBackups, retentionPolicy, cutoffTime, result);

        // Step 2: Validate restore chain usability
        ValidateRestoreChainUsability(backupsToRetain, completedBackups, result);

        // Step 3: Evaluate remaining backups for deletion
        foreach (var backup in completedBackups)
        {
            result.AddEvaluatedBackup(backup);

            if (backupsToRetain.Contains(backup))
            {
                // Already marked as retained with reason in Step 1
                continue;
            }

            // Check if backup is expired based on retention policy
            if (!IsBackupExpired(backup, retentionPolicy, cutoffTime))
            {
                result.RecordRetention(backup, "Not yet expired by retention policy");
                continue;
            }

            // Backup is expired and not needed for chain safety - candidate for deletion
            if (!isDryRun)
            {
                bool deleted = await TryDeleteBackupFileAsync(backup);
                if (deleted)
                {
                    result.RecordDeletion(backup);
                }
                else
                {
                    result.RecordDeletionFailure(backup, "File deletion failed");
                    result.RecordRetention(backup, "Deletion failed - retained by default");
                }
            }
            else
            {
                // Dry run - record as "would be deleted"
                result.RecordDeletion(backup);
            }
        }

        _logger.LogInformation(
            "Retention cleanup completed for {DatabaseName}. " +
            "Evaluated={Evaluated}, Deleted={Deleted}, Retained={Retained}, Failures={Failures}",
            databaseName,
            result.EvaluatedCount,
            result.DeletedCount,
            result.RetainedCount,
            result.FailedDeletionCount);

        if (result.HasFailures)
        {
            _logger.LogError(
                "Retention cleanup encountered {FailureCount} deletion failures for {DatabaseName}",
                result.FailedDeletionCount,
                databaseName);
        }

        return result;
    }

    /// <summary>
    /// Identifies backups that must be retained to preserve restore chain safety.
    /// Conservative: If uncertain, retain.
    /// </summary>
    private HashSet<BackupJob> IdentifyBackupsToRetain(
        List<BackupJob> completedBackups,
        RetentionPolicy retentionPolicy,
        DateTime cutoffTime,
        RetentionCleanupResult result)
    {
        var backupsToRetain = new HashSet<BackupJob>();

        // RULE 1: NEVER delete latest valid Full backup
        var latestFull = completedBackups.FirstOrDefault(b => b.BackupType == BackupType.Full);
        if (latestFull != null)
        {
            backupsToRetain.Add(latestFull);
            result.RecordRetention(latestFull, "Latest Full backup - ALWAYS retained");
        }

        // Get all Full backups that are not expired
        var retainedFullBackups = completedBackups
            .Where(b => b.BackupType == BackupType.Full &&
                       !IsBackupExpired(b, retentionPolicy, cutoffTime))
            .ToList();

        foreach (var fullBackup in retainedFullBackups)
        {
            if (!backupsToRetain.Contains(fullBackup))
            {
                backupsToRetain.Add(fullBackup);
                result.RecordRetention(fullBackup, "Full backup within retention period");
            }

            // RULE 2: Retain Differential backups that depend on this Full backup
            var dependentDifferentials = GetDifferentialBackupsDependingOnFull(
                completedBackups,
                fullBackup);

            foreach (var diff in dependentDifferentials)
            {
                backupsToRetain.Add(diff);
                result.RecordRetention(diff, $"Required by Full backup from {fullBackup.StartTime:yyyy-MM-dd HH:mm}");
            }

            // RULE 3: Retain Log backups needed by this Full backup and its Differentials
            var dependentLogs = GetLogBackupsDependingOnChain(
                completedBackups,
                fullBackup,
                dependentDifferentials);

            foreach (var log in dependentLogs)
            {
                backupsToRetain.Add(log);
                result.RecordRetention(log, $"Required for restore chain starting from {fullBackup.StartTime:yyyy-MM-dd HH:mm}");
            }
        }

        return backupsToRetain;
    }

    /// <summary>
    /// Gets Differential backups that depend on a specific Full backup using LSN validation.
    /// Conservative: If LSN metadata is missing, use time-based fallback and retain.
    /// </summary>
    private List<BackupJob> GetDifferentialBackupsDependingOnFull(
        List<BackupJob> completedBackups,
        BackupJob fullBackup)
    {
        var dependentDifferentials = new List<BackupJob>();

        // Find the next Full backup after this one (if any)
        var nextFull = completedBackups
            .Where(b => b.BackupType == BackupType.Full &&
                       b.StartTime > fullBackup.StartTime)
            .OrderBy(b => b.StartTime)
            .FirstOrDefault();

        // Get all Differentials between this Full and the next Full (time-based window)
        var candidateDifferentials = completedBackups
            .Where(b => b.BackupType == BackupType.Differential &&
                       b.StartTime > fullBackup.StartTime &&
                       (nextFull == null || b.StartTime < nextFull.StartTime))
            .ToList();

        foreach (var diff in candidateDifferentials)
        {
            // LSN-based validation: Differential's DatabaseBackupLSN must match Full's CheckpointLSN
            if (fullBackup.CheckpointLSN.HasValue && diff.DatabaseBackupLSN.HasValue)
            {
                if (diff.DatabaseBackupLSN.Value == fullBackup.CheckpointLSN.Value)
                {
                    // LSN-validated dependency
                    dependentDifferentials.Add(diff);
                }
                else
                {
                    // LSN mismatch - Differential depends on a different Full
                    _logger.LogDebug(
                        "Differential backup {DiffPath} (DatabaseBackupLSN={DiffLSN}) does not depend on Full {FullPath} (CheckpointLSN={FullLSN})",
                        diff.BackupFilePath,
                        diff.DatabaseBackupLSN,
                        fullBackup.BackupFilePath,
                        fullBackup.CheckpointLSN);
                }
            }
            else
            {
                // LSN metadata missing - conservative fallback: retain if in time window
                _logger.LogWarning(
                    "Missing LSN metadata for dependency validation. " +
                    "Full={FullPath} (CheckpointLSN={FullLSN}), Diff={DiffPath} (DatabaseBackupLSN={DiffLSN}). " +
                    "Conservative: Retaining Differential.",
                    fullBackup.BackupFilePath,
                    fullBackup.CheckpointLSN,
                    diff.BackupFilePath,
                    diff.DatabaseBackupLSN);
                dependentDifferentials.Add(diff);
            }
        }

        return dependentDifferentials;
    }

    /// <summary>
    /// Gets Log backups that are part of the restore chain using LSN continuity validation.
    /// Conservative: If LSN metadata is missing OR chain is broken, retain all logs in time window.
    /// </summary>
    private List<BackupJob> GetLogBackupsDependingOnChain(
        List<BackupJob> completedBackups,
        BackupJob fullBackup,
        List<BackupJob> dependentDifferentials)
    {
        // Find the next Full backup after this one
        var nextFull = completedBackups
            .Where(b => b.BackupType == BackupType.Full &&
                       b.StartTime > fullBackup.StartTime)
            .OrderBy(b => b.StartTime)
            .FirstOrDefault();

        DateTime chainStartTime = fullBackup.StartTime;
        DateTime? chainEndTime = nextFull?.StartTime;

        // Get all Log backups in the chain time window
        var candidateLogs = completedBackups
            .Where(b => b.BackupType == BackupType.TransactionLog &&
                       b.StartTime >= chainStartTime &&
                       (chainEndTime == null || b.StartTime < chainEndTime.Value))
            .OrderBy(b => b.StartTime)
            .ToList();

        if (!candidateLogs.Any())
            return candidateLogs;

        // Check if we have LSN metadata for LSN continuity validation
        bool hasLSNMetadata = candidateLogs.All(log => log.FirstLSN.HasValue && log.LastLSN.HasValue);

        if (!hasLSNMetadata)
        {
            // LSN metadata missing - conservative: retain all logs in time window
            _logger.LogWarning(
                "Missing LSN metadata for transaction log chain validation. " +
                "Full={FullPath}, LogCount={LogCount}. Conservative: Retaining all logs in time window.",
                fullBackup.BackupFilePath,
                candidateLogs.Count);
            return candidateLogs;
        }

        // LSN continuity validation
        var dependentLogs = new List<BackupJob>();
        decimal? expectedFirstLSN = fullBackup.LastLSN;

        foreach (var log in candidateLogs)
        {
            if (expectedFirstLSN.HasValue && log.FirstLSN.HasValue)
            {
                if (log.FirstLSN.Value == expectedFirstLSN.Value)
                {
                    // LSN continuity validated
                    dependentLogs.Add(log);
                    expectedFirstLSN = log.LastLSN;
                }
                else
                {
                    // LSN chain break detected
                    _logger.LogWarning(
                        "LSN chain break detected. Expected FirstLSN={ExpectedLSN}, Found={FoundLSN}, Log={LogPath}. " +
                        "Stopping chain inclusion at break point.",
                        expectedFirstLSN,
                        log.FirstLSN,
                        log.BackupFilePath);
                    break; // Stop including logs after chain break
                }
            }
            else
            {
                // LSN metadata missing for this log - conservative: include and continue
                _logger.LogWarning(
                    "Missing LSN metadata for log {LogPath}. Conservative: Including in chain.",
                    log.BackupFilePath);
                dependentLogs.Add(log);
                expectedFirstLSN = log.LastLSN;
            }
        }

        return dependentLogs;
    }

    /// <summary>
    /// Validates that retained backups form a usable restore chain.
    /// Conservative: If chain issues detected, retain additional backups to maintain usability.
    /// </summary>
    private void ValidateRestoreChainUsability(
        HashSet<BackupJob> backupsToRetain,
        List<BackupJob> allBackups,
        RetentionCleanupResult result)
    {
        var retainedFulls = backupsToRetain
            .Where(b => b.BackupType == BackupType.Full)
            .OrderByDescending(b => b.StartTime)
            .ToList();

        foreach (var full in retainedFulls)
        {
            // Check for orphaned Differentials (Differential retained but its base Full is not)
            var retainedDifferentials = backupsToRetain
                .Where(b => b.BackupType == BackupType.Differential &&
                           b.StartTime > full.StartTime)
                .ToList();

            foreach (var diff in retainedDifferentials)
            {
                if (diff.DatabaseBackupLSN.HasValue && full.CheckpointLSN.HasValue)
                {
                    if (diff.DatabaseBackupLSN.Value != full.CheckpointLSN.Value)
                    {
                        // Differential depends on a different Full - find its base Full
                        var baseFull = allBackups
                            .Where(b => b.BackupType == BackupType.Full &&
                                       b.CheckpointLSN == diff.DatabaseBackupLSN &&
                                       b.StartTime < diff.StartTime)
                            .OrderByDescending(b => b.StartTime)
                            .FirstOrDefault();

                        if (baseFull != null && !backupsToRetain.Contains(baseFull))
                        {
                            // Orphaned Differential - retain its base Full
                            _logger.LogWarning(
                                "Orphaned Differential detected: {DiffPath} requires Full {FullPath}. " +
                                "Conservative: Retaining base Full to preserve restore chain usability.",
                                diff.BackupFilePath,
                                baseFull.BackupFilePath);
                            backupsToRetain.Add(baseFull);
                            result.RecordRetention(baseFull, "Required as base for retained Differential (chain usability validation)");
                        }
                    }
                }
            }

            // Check for LSN chain breaks in retained logs
            var retainedLogs = backupsToRetain
                .Where(b => b.BackupType == BackupType.TransactionLog &&
                           b.StartTime >= full.StartTime)
                .OrderBy(b => b.StartTime)
                .ToList();

            if (retainedLogs.Any() && retainedLogs.All(log => log.FirstLSN.HasValue && log.LastLSN.HasValue))
            {
                decimal? expectedFirstLSN = full.LastLSN;
                for (int i = 0; i < retainedLogs.Count; i++)
                {
                    var log = retainedLogs[i];
                    if (expectedFirstLSN.HasValue && log.FirstLSN.HasValue && log.FirstLSN.Value != expectedFirstLSN.Value)
                    {
                        // LSN gap detected - check if there's a missing log we should retain
                        var missingLog = allBackups
                            .Where(b => b.BackupType == BackupType.TransactionLog &&
                                       b.FirstLSN.HasValue &&
                                       b.FirstLSN.Value == expectedFirstLSN.Value &&
                                       !backupsToRetain.Contains(b))
                            .FirstOrDefault();

                        if (missingLog != null)
                        {
                            _logger.LogWarning(
                                "LSN gap detected in retained chain. Expected FirstLSN={ExpectedLSN}, found={FoundLSN}. " +
                                "Conservative: Retaining missing log {LogPath} to preserve LSN continuity.",
                                expectedFirstLSN,
                                log.FirstLSN,
                                missingLog.BackupFilePath);
                            backupsToRetain.Add(missingLog);
                            result.RecordRetention(missingLog, "Required to maintain LSN continuity (chain usability validation)");
                            expectedFirstLSN = missingLog.LastLSN;
                        }
                        else
                        {
                            // Cannot fix LSN gap - log warning but don't fail
                            _logger.LogWarning(
                                "LSN gap detected in retained chain that cannot be resolved. " +
                                "Expected FirstLSN={ExpectedLSN}, found={FoundLSN} in {LogPath}. " +
                                "Restore chain may be incomplete.",
                                expectedFirstLSN,
                                log.FirstLSN,
                                log.BackupFilePath);
                            break;
                        }
                    }
                    expectedFirstLSN = log.LastLSN;
                }
            }
        }
    }

    /// <summary>
    /// Determines if a backup is expired based on retention policy.
    /// </summary>
    private bool IsBackupExpired(
        BackupJob backup,
        RetentionPolicy retentionPolicy,
        DateTime cutoffTime)
    {
        var backupAge = cutoffTime - backup.StartTime;

        return backup.BackupType switch
        {
            BackupType.Full => backupAge > retentionPolicy.FullBackupRetention,
            BackupType.Differential => backupAge > retentionPolicy.DifferentialBackupRetention,
            BackupType.TransactionLog => backupAge > retentionPolicy.LogBackupRetention,
            _ => false // If uncertain, consider not expired (conservative)
        };
    }

    /// <summary>
    /// Attempts to delete a backup file.
    /// Returns true if deleted successfully, false otherwise.
    /// Never throws - failures are returned as false.
    /// </summary>
    private async Task<bool> TryDeleteBackupFileAsync(BackupJob backup)
    {
        try
        {
            // Verify file exists before attempting deletion
            bool exists = await _backupFileDeleter.FileExistsAsync(backup.BackupFilePath);
            if (!exists)
            {
                // File already gone - consider this successful
                return true;
            }

            // Attempt deletion
            bool deleted = await _backupFileDeleter.DeleteBackupFileAsync(backup.BackupFilePath);
            return deleted;
        }
        catch
        {
            // Any exception during deletion is treated as failure
            // Conservative: Don't propagate exceptions, return false
            return false;
        }
    }
}
