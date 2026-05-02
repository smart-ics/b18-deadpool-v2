using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

/// <summary>
/// Phase 2 foundation: builds restore plans for STOPAT restore execution.
/// Planning only - does not execute restore operations.
/// </summary>
public sealed class RestorePlannerService : IRestorePlannerService
{
    private readonly IBackupJobRepository _backupJobRepository;

    public RestorePlannerService(IBackupJobRepository backupJobRepository)
    {
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
    }

    public async Task<RestorePlan> BuildRestorePlanAsync(string databaseName, DateTime targetTime)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        var completedBackups = (await _backupJobRepository.GetBackupsByDatabaseAsync(databaseName))
            .Where(b => b.Status == BackupStatus.Completed)
            .Where(b => b.EndTime.HasValue)
            .OrderBy(b => b.StartTime)
            .ToList();

        var fullBackup = SelectFullBackup(completedBackups, targetTime);
        if (fullBackup == null)
        {
            return RestorePlan.CreateInvalidPlan(
                databaseName,
                targetTime,
                "No valid Full backup found before target time.");
        }

        var differentialBackup = SelectDifferentialBackup(completedBackups, fullBackup, targetTime);
        var baseBackup = differentialBackup ?? fullBackup;
        var logSelection = SelectLogBackups(completedBackups, baseBackup, targetTime);

        if (!logSelection.IsValid)
        {
            return RestorePlan.CreateInvalidPlan(databaseName, targetTime, logSelection.FailureReason!);
        }

        return RestorePlan.CreateValidPlan(
            databaseName,
            targetTime,
            fullBackup,
            differentialBackup,
            logSelection.LogBackups,
            logSelection.ActualRestorePoint);
    }

    private static BackupJob? SelectFullBackup(List<BackupJob> completedBackups, DateTime targetTime)
    {
        return completedBackups
            .Where(b => b.BackupType == BackupType.Full)
            .Where(b => b.EndTime!.Value <= targetTime)
            .OrderByDescending(b => b.EndTime)
            .FirstOrDefault();
    }

    private static BackupJob? SelectDifferentialBackup(
        List<BackupJob> completedBackups,
        BackupJob fullBackup,
        DateTime targetTime)
    {
        // Reuse existing LSN dependency rule: Differential.DatabaseBackupLSN must match Full.CheckpointLSN.
        if (!fullBackup.CheckpointLSN.HasValue)
            return null;

        return completedBackups
            .Where(b => b.BackupType == BackupType.Differential)
            .Where(b => b.EndTime!.Value > fullBackup.EndTime!.Value)
            .Where(b => b.EndTime!.Value <= targetTime)
            .Where(b => b.DatabaseBackupLSN.HasValue && b.DatabaseBackupLSN.Value == fullBackup.CheckpointLSN.Value)
            .OrderByDescending(b => b.EndTime)
            .FirstOrDefault();
    }

    private static (bool IsValid, string? FailureReason, List<BackupJob> LogBackups, DateTime ActualRestorePoint) SelectLogBackups(
        List<BackupJob> completedBackups,
        BackupJob baseBackup,
        DateTime targetTime)
    {
        var baseEndTime = baseBackup.EndTime!.Value;

        if (targetTime <= baseEndTime)
        {
            return (true, null, new List<BackupJob>(), baseEndTime);
        }

        var candidateLogs = completedBackups
            .Where(b => b.BackupType == BackupType.TransactionLog)
            .Where(b => b.StartTime >= baseEndTime)
            .OrderBy(b => b.StartTime)
            .ToList();

        if (!candidateLogs.Any())
        {
            return (
                false,
                "Restore target requires transaction log backups, but none are available after the selected base backup.",
                new List<BackupJob>(),
                baseEndTime);
        }

        if (!baseBackup.LastLSN.HasValue)
        {
            return (
                false,
                "Selected base backup is missing LastLSN, so log chain continuity cannot be validated.",
                new List<BackupJob>(),
                baseEndTime);
        }

        var selectedLogs = new List<BackupJob>();
        var expectedFirstLsn = baseBackup.LastLSN.Value;

        foreach (var log in candidateLogs)
        {
            if (!log.FirstLSN.HasValue || !log.LastLSN.HasValue)
            {
                return (
                    false,
                    $"Log backup is missing LSN metadata: {log.BackupFilePath}",
                    new List<BackupJob>(),
                    baseEndTime);
            }

            if (log.FirstLSN.Value > expectedFirstLsn)
            {
                return (
                    false,
                    $"Broken transaction log chain detected. Expected FirstLSN <= {expectedFirstLsn}, found {log.FirstLSN.Value}.",
                    new List<BackupJob>(),
                    baseEndTime);
            }

            selectedLogs.Add(log);
            expectedFirstLsn = log.LastLSN.Value;

            if (log.EndTime!.Value >= targetTime)
            {
                return (true, null, selectedLogs, targetTime);
            }
        }

        return (
            false,
            $"Restore target {targetTime:yyyy-MM-dd HH:mm:ss} is beyond available log coverage (last log ends at {selectedLogs.Last().EndTime:yyyy-MM-dd HH:mm:ss}).",
            new List<BackupJob>(),
            baseEndTime);
    }
}
