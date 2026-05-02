using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Core.Services;

/// <summary>
/// Validates restore plans for physical file readiness and defensive chain consistency.
/// Validation only - does not execute restore operations.
/// </summary>
public sealed class RestorePlanValidatorService : IRestorePlanValidatorService
{
    private const string PendingPathPrefix = "PENDING_";

    private readonly ILogger<RestorePlanValidatorService> _logger;

    public RestorePlanValidatorService(ILogger<RestorePlanValidatorService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RestoreValidationResult Validate(RestorePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var result = new RestoreValidationResult();
        var orderedLogs = NormalizeAndValidateLogOrder(plan, result);

        if (!plan.IsValid)
        {
            var message = $"Restore plan is invalid: {plan.FailureReason ?? "Unknown failure reason."}";
            result.AddError(message);
            _logger.LogError("Restore plan validation failed: {Reason}", message);
            return result;
        }

        if (plan.FullBackup == null)
        {
            const string message = "Restore plan does not contain a Full backup.";
            result.AddError(message);
            _logger.LogError("Chain inconsistency: {Message}", message);
            return result;
        }

        ValidateFileSet(plan, orderedLogs, result);
        ValidateChainConsistency(plan, orderedLogs, result);
        ValidateStopAtCoverage(plan, orderedLogs, result);

        return result;
    }

    private void ValidateFileSet(RestorePlan plan, IReadOnlyList<BackupJob> orderedLogs, RestoreValidationResult result)
    {
        var orderedBackups = BuildOrderedBackups(plan, orderedLogs);

        foreach (var backup in orderedBackups)
        {
            var displayPath = GetDisplayPath(backup);

            if (!IsUsablePath(backup.BackupFilePath))
            {
                var message = $"Backup file path is missing or pending placeholder for {backup.BackupType} backup: {displayPath}.";
                result.AddError(message);
                _logger.LogError("Missing file: {Message}", message);
                continue;
            }

            if (!File.Exists(backup.BackupFilePath))
            {
                var message = $"Backup file does not exist for {backup.BackupType} backup: {backup.BackupFilePath}.";
                result.AddError(message);
                _logger.LogError("Missing file: {Message}", message);
                continue;
            }

            try
            {
                using var stream = new FileStream(
                    backup.BackupFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                if (!stream.CanRead)
                {
                    var message = $"Backup file is not readable for {backup.BackupType} backup: {backup.BackupFilePath}.";
                    result.AddError(message);
                    _logger.LogError("Inaccessible file: {Message}", message);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                var message = $"Access denied while opening backup file for {backup.BackupType} backup: {backup.BackupFilePath}.";
                result.AddError(message);
                _logger.LogError(ex, "Inaccessible file: {Message}", message);
            }
            catch (IOException ex)
            {
                var message = $"Backup file is inaccessible or locked for {backup.BackupType} backup: {backup.BackupFilePath}.";
                result.AddError(message);
                _logger.LogError(ex, "Inaccessible file: {Message}", message);
            }
        }
    }

    private void ValidateChainConsistency(RestorePlan plan, IReadOnlyList<BackupJob> orderedLogs, RestoreValidationResult result)
    {
        var fullBackup = plan.FullBackup!;

        if (fullBackup.BackupType != BackupType.Full)
        {
            var message = "Restore plan FullBackup entry is not a Full backup.";
            result.AddError(message);
            _logger.LogError("Chain inconsistency: {Message}", message);
        }

        var differential = plan.DifferentialBackup;
        if (differential != null)
        {
            if (differential.BackupType != BackupType.Differential)
            {
                var message = "Restore plan DifferentialBackup entry is not a Differential backup.";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
            }

            if (!differential.DatabaseBackupLSN.HasValue)
            {
                var message = "Differential backup missing DatabaseBackupLSN - cannot validate dependency.";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
            }
            else if (!fullBackup.CheckpointLSN.HasValue)
            {
                var message = "Full backup missing CheckpointLSN - cannot validate Differential dependency.";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
            }
            else if (differential.DatabaseBackupLSN.Value != fullBackup.CheckpointLSN.Value)
            {
                var message = "Differential backup does not depend on selected Full backup (LSN mismatch).";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
            }

            if (!fullBackup.EndTime.HasValue || !differential.EndTime.HasValue || differential.EndTime.Value < fullBackup.EndTime.Value)
            {
                var message = "Differential backup timing is inconsistent with Full backup sequence.";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
            }
        }

        ValidateLogChain(plan, orderedLogs, result);
    }

    private void ValidateLogChain(RestorePlan plan, IReadOnlyList<BackupJob> orderedLogs, RestoreValidationResult result)
    {
        var logs = orderedLogs;
        if (logs.Count == 0)
            return;

        var baseBackup = plan.DifferentialBackup ?? plan.FullBackup!;
        if (!baseBackup.EndTime.HasValue)
        {
            var message = "Base backup missing EndTime.";
            result.AddError(message);
            _logger.LogError("Chain inconsistency: {Message}", message);
            return;
        }

        DateTime? previousEndTime = baseBackup.EndTime;
        BackupJob? previousLog = null;

        foreach (var log in logs)
        {
            if (log.BackupType != BackupType.TransactionLog)
            {
                var message = $"Restore plan log sequence contains non-log backup type: {log.BackupType}.";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
                continue;
            }

            if (!log.EndTime.HasValue)
            {
                var message = $"Transaction log backup is missing EndTime: {GetDisplayPath(log)}.";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
                continue;
            }

            if (previousEndTime.HasValue && log.StartTime < previousEndTime.Value)
            {
                var message = $"Transaction log backup ordering is inconsistent at {log.StartTime:yyyy-MM-dd HH:mm:ss}.";
                result.AddError(message);
                _logger.LogError("Chain inconsistency: {Message}", message);
            }

            if (previousLog != null)
            {
                if (!previousLog.LastLSN.HasValue || !log.FirstLSN.HasValue)
                {
                    var message =
                        $"Log chain is missing LSN metadata between consecutive logs: {GetDisplayPath(previousLog)} -> {GetDisplayPath(log)}.";
                    result.AddError(message);
                    _logger.LogError("Chain inconsistency: {Message}", message);
                    previousEndTime = log.EndTime;
                    previousLog = log;
                    continue;
                }

                if (previousLog.LastLSN.Value != log.FirstLSN.Value)
                {
                    var message =
                        $"Broken transaction log chain detected. Expected FirstLSN == {previousLog.LastLSN.Value}, found {log.FirstLSN.Value}.";
                    result.AddError(message);
                    _logger.LogError("Chain inconsistency: {Message}", message);
                }
            }

            previousEndTime = log.EndTime;
            previousLog = log;
        }
    }

    private void ValidateStopAtCoverage(RestorePlan plan, IReadOnlyList<BackupJob> orderedLogs, RestoreValidationResult result)
    {
        var baseBackup = plan.DifferentialBackup ?? plan.FullBackup!;
        var targetTime = plan.TargetTime;

        if (!baseBackup.EndTime.HasValue)
        {
            const string message = "Selected base backup is missing EndTime, STOPAT coverage cannot be validated.";
            result.AddError(message);
            _logger.LogError("STOPAT coverage failure: {Message}", message);
            return;
        }

        if (targetTime <= baseBackup.EndTime.Value)
            return;

        if (orderedLogs.Count == 0)
        {
            var message = $"Restore target {targetTime:yyyy-MM-dd HH:mm:ss} requires transaction log backups, but none are available.";
            result.AddError(message);
            _logger.LogError("STOPAT coverage failure: {Message}", message);
            return;
        }

        var firstLog = orderedLogs.First();
        var lastLog = orderedLogs.Last();

        if (targetTime < firstLog.StartTime)
        {
            var message =
                $"Restore target {targetTime:yyyy-MM-dd HH:mm:ss} is before the first log backup starts " +
                $"(first log starts at {firstLog.StartTime:yyyy-MM-dd HH:mm:ss}).";
            result.AddError(message);
            _logger.LogError("STOPAT coverage failure: {Message}", message);
        }

        if (!lastLog.EndTime.HasValue)
        {
            var message = $"Last log backup is missing EndTime: {GetDisplayPath(lastLog)}.";
            result.AddError(message);
            _logger.LogError("STOPAT coverage failure: {Message}", message);
            return;
        }

        if (targetTime > lastLog.EndTime.Value)
        {
            var message =
                $"Restore target {targetTime:yyyy-MM-dd HH:mm:ss} is beyond available log backup coverage " +
                $"(last log ends at {lastLog.EndTime:yyyy-MM-dd HH:mm:ss}).";
            result.AddError(message);
            _logger.LogError("STOPAT coverage failure: {Message}", message);
        }
    }

    private List<BackupJob> NormalizeAndValidateLogOrder(RestorePlan plan, RestoreValidationResult result)
    {
        var inputLogs = plan.LogBackups.ToList();

        var orderedLogs = inputLogs
            .OrderBy(l => l.StartTime)
            .ThenBy(l => l.EndTime ?? DateTime.MaxValue)
            .ThenBy(l => l.BackupFilePath, StringComparer.Ordinal)
            .ToList();

        if (!inputLogs.SequenceEqual(orderedLogs))
        {
            const string message = "Transaction log backups are not in chronological order. Validation requires deterministic ordering.";
            result.AddError(message);
            _logger.LogError("Chain inconsistency: {Message}", message);
        }

        return orderedLogs;
    }

    private static List<BackupJob> BuildOrderedBackups(RestorePlan plan, IReadOnlyList<BackupJob> orderedLogs)
    {
        var backups = new List<BackupJob>
        {
            plan.FullBackup!
        };

        if (plan.DifferentialBackup != null)
            backups.Add(plan.DifferentialBackup);

        backups.AddRange(orderedLogs);
        return backups;
    }

    private static bool IsUsablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return !path.StartsWith(PendingPathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDisplayPath(BackupJob backup)
    {
        return string.IsNullOrWhiteSpace(backup.BackupFilePath) ? "<missing path>" : backup.BackupFilePath;
    }
}
