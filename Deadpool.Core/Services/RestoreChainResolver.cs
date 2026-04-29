using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

/// <summary>
/// Resolves backup restore chains using LSN-aware logic.
/// Planning only - does not execute restore operations.
/// </summary>
public class RestoreChainResolver : IRestoreChainResolver
{
    private readonly IBackupJobRepository _backupJobRepository;

    public RestoreChainResolver(IBackupJobRepository backupJobRepository)
    {
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
    }

    public async Task<RestorePlan> ResolveRestoreChainAsync(string databaseName, DateTime restorePoint)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        // Get all completed backups for the database
        var allBackups = await _backupJobRepository.GetBackupsByDatabaseAsync(databaseName);
        var completedBackups = allBackups
            .Where(b => b.Status == BackupStatus.Completed)
            .OrderBy(b => b.StartTime)
            .ToList();

        if (!completedBackups.Any())
        {
            return RestorePlan.CreateInvalidPlan(
                databaseName,
                restorePoint,
                "No completed backups found for database.");
        }

        // Step 1: Select Full backup
        var fullBackup = SelectFullBackup(completedBackups, restorePoint);
        if (fullBackup == null)
        {
            return RestorePlan.CreateInvalidPlan(
                databaseName,
                restorePoint,
                "No valid Full backup found before restore point.");
        }

        // Step 2: Select Differential backup (optional)
        var differentialBackup = SelectDifferentialBackup(completedBackups, fullBackup, restorePoint);

        // Step 3: Select Log backup chain
        var baseBackup = differentialBackup ?? fullBackup;
        var logBackups = SelectLogBackups(completedBackups, baseBackup, restorePoint);

        // Step 4: Validate chain
        var validationResult = ValidateRestoreChain(fullBackup, differentialBackup, logBackups, restorePoint);
        if (!validationResult.IsValid)
        {
            return RestorePlan.CreateInvalidPlan(
                databaseName,
                restorePoint,
                validationResult.FailureReason!);
        }

        // Step 5: Determine actual restore point
        var actualRestorePoint = DetermineActualRestorePoint(baseBackup, logBackups, restorePoint);

        return RestorePlan.CreateValidPlan(
            databaseName,
            restorePoint,
            fullBackup,
            differentialBackup,
            logBackups,
            actualRestorePoint);
    }

    /// <summary>
    /// Select the latest Full backup that completed before the restore point.
    /// </summary>
    private BackupJob? SelectFullBackup(List<BackupJob> completedBackups, DateTime restorePoint)
    {
        return completedBackups
            .Where(b => b.BackupType == BackupType.Full)
            .Where(b => b.EndTime.HasValue && b.EndTime.Value <= restorePoint)
            .OrderByDescending(b => b.EndTime)
            .FirstOrDefault();
    }

    /// <summary>
    /// Select the latest Differential backup that:
    /// - Completed before the restore point
    /// - Depends on the selected Full backup (matching DatabaseBackupLSN)
    /// </summary>
    private BackupJob? SelectDifferentialBackup(
        List<BackupJob> completedBackups,
        BackupJob fullBackup,
        DateTime restorePoint)
    {
        // Differential requires LSN metadata to validate dependency
        if (!fullBackup.CheckpointLSN.HasValue)
            return null; // Cannot validate differential without Full's CheckpointLSN

        return completedBackups
            .Where(b => b.BackupType == BackupType.Differential)
            .Where(b => b.EndTime.HasValue && b.EndTime.Value <= restorePoint)
            .Where(b => b.DatabaseBackupLSN.HasValue && b.DatabaseBackupLSN.Value == fullBackup.CheckpointLSN.Value)
            .OrderByDescending(b => b.EndTime)
            .FirstOrDefault();
    }

    /// <summary>
    /// Select the minimal log backup chain required to reach the restore point.
    /// Logs must form a continuous LSN chain from the base backup.
    /// </summary>
    private List<BackupJob> SelectLogBackups(
        List<BackupJob> completedBackups,
        BackupJob baseBackup,
        DateTime restorePoint)
    {
        // If no LSN metadata, cannot validate log chain - return empty
        if (!baseBackup.LastLSN.HasValue)
            return new List<BackupJob>();

        var logBackups = completedBackups
            .Where(b => b.BackupType == BackupType.TransactionLog)
            .Where(b => b.EndTime.HasValue && b.StartTime >= baseBackup.EndTime!.Value)
            .Where(b => b.FirstLSN.HasValue && b.LastLSN.HasValue)
            .OrderBy(b => b.StartTime)
            .ToList();

        var selectedLogs = new List<BackupJob>();
        var currentLSN = baseBackup.LastLSN.Value;

        foreach (var log in logBackups)
        {
            // Log must continue from current LSN
            if (log.FirstLSN!.Value > currentLSN)
                break; // LSN gap detected

            selectedLogs.Add(log);
            currentLSN = log.LastLSN!.Value;

            // Stop if log covers the restore point
            if (log.EndTime!.Value >= restorePoint)
                break;
        }

        return selectedLogs;
    }

    /// <summary>
    /// Validate that the restore chain is internally consistent.
    /// </summary>
    private (bool IsValid, string? FailureReason) ValidateRestoreChain(
        BackupJob fullBackup,
        BackupJob? differentialBackup,
        List<BackupJob> logBackups,
        DateTime restorePoint)
    {
        // Validate Full backup has required LSN metadata
        if (!fullBackup.LastLSN.HasValue)
        {
            return (false, "Full backup missing LSN metadata - cannot validate restore chain.");
        }

        // Validate Differential dependency if present
        if (differentialBackup != null)
        {
            if (!differentialBackup.DatabaseBackupLSN.HasValue)
            {
                return (false, "Differential backup missing DatabaseBackupLSN - cannot validate dependency.");
            }

            if (!fullBackup.CheckpointLSN.HasValue)
            {
                return (false, "Full backup missing CheckpointLSN - cannot validate Differential dependency.");
            }

            if (differentialBackup.DatabaseBackupLSN.Value != fullBackup.CheckpointLSN.Value)
            {
                return (false, "Differential backup does not depend on selected Full backup (LSN mismatch).");
            }
        }

        // Validate Log chain continuity and coverage
        if (logBackups.Any())
        {
            var baseBackup = differentialBackup ?? fullBackup;
            var currentLSN = baseBackup.LastLSN!.Value;

            foreach (var log in logBackups)
            {
                if (!log.FirstLSN.HasValue || !log.LastLSN.HasValue)
                {
                    return (false, $"Log backup missing LSN metadata: {log.BackupFilePath}");
                }

                if (log.FirstLSN.Value > currentLSN)
                {
                    return (false, $"LSN gap detected in log chain at {log.StartTime:yyyy-MM-dd HH:mm:ss}");
                }

                currentLSN = log.LastLSN.Value;
            }

            // Validate restore point is covered by the selected log chain
            var lastLog = logBackups.Last();

            // Check if restore point is beyond the last selected log's coverage
            if (restorePoint > lastLog.EndTime!.Value)
            {
                return (false, 
                    $"Restore point {restorePoint:yyyy-MM-dd HH:mm:ss} is beyond available log backup coverage " +
                    $"(last log ends at {lastLog.EndTime:yyyy-MM-dd HH:mm:ss}).");
            }

            // Check if restore point is before the first log starts
            var firstLog = logBackups.First();
            if (restorePoint < firstLog.StartTime)
            {
                return (false,
                    $"Restore point {restorePoint:yyyy-MM-dd HH:mm:ss} is before the first log backup starts " +
                    $"(first log starts at {firstLog.StartTime:yyyy-MM-dd HH:mm:ss}).");
            }
        }
        else
        {
            // No logs - can only restore to base backup completion time
            var baseBackup = differentialBackup ?? fullBackup;
            if (baseBackup.EndTime!.Value < restorePoint)
            {
                return (false, $"Restore point {restorePoint:yyyy-MM-dd HH:mm:ss} requires log backups, but none are available.");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Determine the actual restore point that will be achieved.
    /// For point-in-time restore with logs: returns the requested point (STOPAT semantics).
    /// For Full/Diff only: returns the backup completion time.
    /// </summary>
    private DateTime DetermineActualRestorePoint(
        BackupJob baseBackup,
        List<BackupJob> logBackups,
        DateTime requestedRestorePoint)
    {
        if (logBackups.Any())
        {
            // Point-in-time restore: return requested point
            // (Execution phase will use RESTORE LOG ... WITH STOPAT = requestedRestorePoint)
            return requestedRestorePoint;
        }

        // No logs - can only restore to base backup completion time
        return baseBackup.EndTime!.Value;
    }
}
