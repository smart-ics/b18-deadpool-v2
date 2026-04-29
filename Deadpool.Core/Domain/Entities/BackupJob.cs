using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

public class BackupJob
{
    public string DatabaseName { get; }
    public BackupType BackupType { get; }
    public BackupStatus Status { get; private set; }
    public DateTime StartTime { get; }
    public DateTime? EndTime { get; private set; }
    public string BackupFilePath { get; }
    public long? FileSizeBytes { get; private set; }
    public string? ErrorMessage { get; private set; }

    // LSN metadata for restore chain validation
    // Captured from msdb.dbo.backupset after backup completion
    public decimal? FirstLSN { get; private set; }
    public decimal? LastLSN { get; private set; }
    public decimal? DatabaseBackupLSN { get; private set; } // For Differential: LSN of base Full backup
    public decimal? CheckpointLSN { get; private set; } // For Full: checkpoint LSN that Differentials reference

    public BackupJob(
        string databaseName,
        BackupType backupType,
        string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        DatabaseName = databaseName;
        BackupType = backupType;
        BackupFilePath = backupFilePath;
        Status = BackupStatus.Pending;
        StartTime = DateTime.UtcNow;
    }

    public void MarkAsRunning()
    {
        if (Status != BackupStatus.Pending)
            throw new InvalidOperationException($"Cannot mark job as running. Current status: {Status}");

        Status = BackupStatus.Running;
    }

    public void MarkAsCompleted(long fileSizeBytes)
    {
        if (Status != BackupStatus.Running)
            throw new InvalidOperationException($"Cannot mark job as completed. Current status: {Status}");

        if (fileSizeBytes <= 0)
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSizeBytes));

        Status = BackupStatus.Completed;
        EndTime = DateTime.UtcNow;
        FileSizeBytes = fileSizeBytes;
    }

    /// <summary>
    /// Sets LSN metadata for restore chain validation.
    /// Must be called after backup completion.
    /// </summary>
    public void SetLSNMetadata(decimal? firstLSN, decimal? lastLSN, decimal? databaseBackupLSN, decimal? checkpointLSN)
    {
        if (Status != BackupStatus.Completed)
            throw new InvalidOperationException("LSN metadata can only be set for completed backups.");

        FirstLSN = firstLSN;
        LastLSN = lastLSN;
        DatabaseBackupLSN = databaseBackupLSN;
        CheckpointLSN = checkpointLSN;
    }

    public void MarkAsFailed(string errorMessage)
    {
        if (Status == BackupStatus.Completed)
            throw new InvalidOperationException("Cannot mark completed job as failed.");

        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

        Status = BackupStatus.Failed;
        EndTime = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    public TimeSpan? GetDuration()
    {
        if (!EndTime.HasValue)
            return null;

        return EndTime.Value - StartTime;
    }
}
