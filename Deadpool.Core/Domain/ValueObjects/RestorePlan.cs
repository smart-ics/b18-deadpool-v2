using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Represents a validated restore plan with selected backup chain and sequence.
/// This is planning only - not execution.
/// </summary>
public sealed class RestorePlan
{
    public bool IsValid { get; }
    public string? FailureReason { get; }

    public BackupJob? FullBackup { get; }
    public BackupJob? DifferentialBackup { get; }
    public IReadOnlyList<BackupJob> LogBackups { get; }

    public DateTime RequestedRestorePoint { get; }
    public DateTime TargetTime => RequestedRestorePoint;
    public DateTime? ActualRestorePoint { get; }

    public string DatabaseName { get; }

    /// <summary>
    /// Ordered restore sequence: Full -> [Diff] -> [Logs...]
    /// </summary>
    public IReadOnlyList<BackupJob> RestoreSequence { get; }

    private RestorePlan(
        string databaseName,
        DateTime requestedRestorePoint,
        BackupJob? fullBackup,
        BackupJob? differentialBackup,
        IReadOnlyList<BackupJob> logBackups,
        DateTime? actualRestorePoint,
        bool isValid,
        string? failureReason)
    {
        DatabaseName = databaseName;
        RequestedRestorePoint = requestedRestorePoint;
        FullBackup = fullBackup;
        DifferentialBackup = differentialBackup;
        LogBackups = logBackups;
        ActualRestorePoint = actualRestorePoint;
        IsValid = isValid;
        FailureReason = failureReason;

        RestoreSequence = BuildRestoreSequence();
    }

    private IReadOnlyList<BackupJob> BuildRestoreSequence()
    {
        if (!IsValid)
            return Array.Empty<BackupJob>();

        var sequence = new List<BackupJob>();

        if (FullBackup != null)
            sequence.Add(FullBackup);

        if (DifferentialBackup != null)
            sequence.Add(DifferentialBackup);

        sequence.AddRange(LogBackups);

        return sequence;
    }

    public static RestorePlan CreateValidPlan(
        string databaseName,
        DateTime requestedRestorePoint,
        BackupJob fullBackup,
        BackupJob? differentialBackup,
        IReadOnlyList<BackupJob> logBackups,
        DateTime actualRestorePoint)
    {
        if (fullBackup == null)
            throw new ArgumentNullException(nameof(fullBackup));

        return new RestorePlan(
            databaseName,
            requestedRestorePoint,
            fullBackup,
            differentialBackup,
            logBackups ?? Array.Empty<BackupJob>(),
            actualRestorePoint,
            isValid: true,
            failureReason: null);
    }

    public static RestorePlan CreateInvalidPlan(
        string databaseName,
        DateTime requestedRestorePoint,
        string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason must be provided for invalid plan.", nameof(failureReason));

        return new RestorePlan(
            databaseName,
            requestedRestorePoint,
            fullBackup: null,
            differentialBackup: null,
            logBackups: Array.Empty<BackupJob>(),
            actualRestorePoint: null,
            isValid: false,
            failureReason: failureReason);
    }

    public string GetRestoreDescription()
    {
        if (!IsValid)
            return $"Invalid restore plan: {FailureReason}";

        // Determine if this is a point-in-time restore (STOPAT): the actual restore
        // point falls within a log file rather than at the end of the last log.
        var isPointInTime = LogBackups.Count > 0
            && ActualRestorePoint.HasValue
            && ActualRestorePoint.Value < LogBackups[LogBackups.Count - 1].EndTime;

        var description = isPointInTime
            ? $"Point-in-time restore of {DatabaseName} to {ActualRestorePoint:yyyy-MM-dd HH:mm:ss}\n"
            : $"Restore {DatabaseName} to {ActualRestorePoint:yyyy-MM-dd HH:mm:ss}\n";
        description += $"Sequence: {RestoreSequence.Count} backup(s)\n";
        description += $"- Full: {FullBackup?.StartTime:yyyy-MM-dd HH:mm:ss}\n";

        if (DifferentialBackup != null)
            description += $"- Differential: {DifferentialBackup.StartTime:yyyy-MM-dd HH:mm:ss}\n";

        if (LogBackups.Any())
            description += $"- Logs: {LogBackups.Count} file(s)\n";

        return description;
    }
}
