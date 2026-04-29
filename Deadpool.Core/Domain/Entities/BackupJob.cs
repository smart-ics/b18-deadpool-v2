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

    // Copy tracking
    public bool CopyStarted { get; private set; }
    public bool CopyCompleted { get; private set; }
    public DateTime? CopyStartTime { get; private set; }
    public DateTime? CopyEndTime { get; private set; }
    public string? CopyDestinationPath { get; private set; }
    public string? CopyErrorMessage { get; private set; }

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

    public void MarkCopyStarted(string destinationPath)
    {
        if (Status != BackupStatus.Completed)
            throw new InvalidOperationException($"Cannot start copy for non-completed backup. Current status: {Status}");

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationPath));

        CopyStarted = true;
        CopyStartTime = DateTime.UtcNow;
        CopyDestinationPath = destinationPath;
    }

    public void MarkCopyCompleted()
    {
        if (!CopyStarted)
            throw new InvalidOperationException("Cannot mark copy completed when copy has not started.");

        CopyCompleted = true;
        CopyEndTime = DateTime.UtcNow;
    }

    public void MarkCopyFailed(string errorMessage)
    {
        if (!CopyStarted)
            throw new InvalidOperationException("Cannot mark copy failed when copy has not started.");

        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

        CopyCompleted = false;
        CopyEndTime = DateTime.UtcNow;
        CopyErrorMessage = errorMessage;
    }

    public TimeSpan? GetCopyDuration()
    {
        if (!CopyStartTime.HasValue || !CopyEndTime.HasValue)
            return null;

        return CopyEndTime.Value - CopyStartTime.Value;
    }
}
