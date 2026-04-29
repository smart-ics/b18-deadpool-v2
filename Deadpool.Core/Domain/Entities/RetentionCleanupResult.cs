using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

/// <summary>
/// Represents the result of a retention cleanup operation.
/// Tracks what was evaluated, deleted, retained, and any failures.
/// </summary>
public class RetentionCleanupResult
{
    public string DatabaseName { get; }
    public DateTime CleanupTime { get; }
    public bool IsDryRun { get; }

    private readonly List<BackupJob> _evaluatedBackups = new();
    private readonly List<BackupJob> _deletedBackups = new();
    private readonly List<BackupJob> _retainedBackups = new();
    private readonly List<string> _deletionFailures = new();
    private readonly List<string> _safetyReasons = new();

    public IReadOnlyList<BackupJob> EvaluatedBackups => _evaluatedBackups.AsReadOnly();
    public IReadOnlyList<BackupJob> DeletedBackups => _deletedBackups.AsReadOnly();
    public IReadOnlyList<BackupJob> RetainedBackups => _retainedBackups.AsReadOnly();
    public IReadOnlyList<string> DeletionFailures => _deletionFailures.AsReadOnly();
    public IReadOnlyList<string> SafetyReasons => _safetyReasons.AsReadOnly();

    public int EvaluatedCount => _evaluatedBackups.Count;
    public int DeletedCount => _deletedBackups.Count;
    public int RetainedCount => _retainedBackups.Count;
    public int FailedDeletionCount => _deletionFailures.Count;

    public bool HasFailures => _deletionFailures.Any();
    public bool IsSuccessful => !HasFailures;

    public RetentionCleanupResult(string databaseName, bool isDryRun = false)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        DatabaseName = databaseName;
        CleanupTime = DateTime.UtcNow;
        IsDryRun = isDryRun;
    }

    public void AddEvaluatedBackup(BackupJob backup)
    {
        if (backup == null)
            throw new ArgumentNullException(nameof(backup));

        _evaluatedBackups.Add(backup);
    }

    public void RecordDeletion(BackupJob backup)
    {
        if (backup == null)
            throw new ArgumentNullException(nameof(backup));

        _deletedBackups.Add(backup);
    }

    public void RecordRetention(BackupJob backup, string reason)
    {
        if (backup == null)
            throw new ArgumentNullException(nameof(backup));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Retention reason cannot be empty.", nameof(reason));

        _retainedBackups.Add(backup);
        _safetyReasons.Add($"{backup.BackupType} backup from {backup.StartTime:yyyy-MM-dd HH:mm}: {reason}");
    }

    public void RecordDeletionFailure(BackupJob backup, string errorMessage)
    {
        if (backup == null)
            throw new ArgumentNullException(nameof(backup));

        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

        _deletionFailures.Add($"Failed to delete {backup.BackupType} backup {backup.BackupFilePath}: {errorMessage}");
    }

    public string GetSummary()
    {
        var mode = IsDryRun ? "DRY RUN" : "ACTUAL";
        return $"Retention cleanup for {DatabaseName} ({mode}): " +
               $"Evaluated={EvaluatedCount}, Deleted={DeletedCount}, " +
               $"Retained={RetainedCount}, Failed={FailedDeletionCount}";
    }
}
