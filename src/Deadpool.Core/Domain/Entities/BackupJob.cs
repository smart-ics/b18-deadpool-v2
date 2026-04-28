using Deadpool.Core.Domain.Common;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

/// <summary>
/// Represents a scheduled or on-demand backup job execution
/// </summary>
public class BackupJob : Entity
{
    public Guid DatabaseId { get; private set; }
    public Guid? BackupScheduleId { get; private set; }
    public BackupType BackupType { get; private set; }
    public BackupStatus Status { get; private set; }
    public DateTime ScheduledStartTime { get; private set; }
    public DateTime? ActualStartTime { get; private set; }
    public DateTime? CompletedTime { get; private set; }
    public string BackupFilePath { get; private set; }
    public long? BackupSizeInBytes { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int? RetryCount { get; private set; }
    public bool IsCompressed { get; private set; }
    public bool IsEncrypted { get; private set; }
    public int? CompressionPercentage { get; private set; }
    public TimeSpan? Duration { get; private set; }

    private BackupJob() : base()
    {
        BackupFilePath = string.Empty;
    }

    public BackupJob(
        Guid databaseId,
        BackupType backupType,
        string backupFilePath,
        DateTime scheduledStartTime,
        Guid? backupScheduleId = null,
        bool isCompressed = true,
        bool isEncrypted = false) : base()
    {
        DatabaseId = databaseId;
        BackupScheduleId = backupScheduleId;
        BackupType = backupType;
        BackupFilePath = backupFilePath ?? throw new ArgumentNullException(nameof(backupFilePath));
        ScheduledStartTime = scheduledStartTime;
        Status = BackupStatus.Pending;
        IsCompressed = isCompressed;
        IsEncrypted = isEncrypted;
        RetryCount = 0;
    }

    public void Start()
    {
        if (Status != BackupStatus.Pending)
            throw new InvalidOperationException($"Cannot start job in {Status} status");

        Status = BackupStatus.Running;
        ActualStartTime = DateTime.UtcNow;
    }

    public void Complete(long backupSizeInBytes, int? compressionPercentage = null)
    {
        if (Status != BackupStatus.Running)
            throw new InvalidOperationException($"Cannot complete job not in Running status");

        Status = BackupStatus.Completed;
        CompletedTime = DateTime.UtcNow;
        BackupSizeInBytes = backupSizeInBytes;
        CompressionPercentage = compressionPercentage;
        
        if (ActualStartTime.HasValue)
        {
            Duration = CompletedTime.Value - ActualStartTime.Value;
        }
    }

    public void Fail(string errorMessage)
    {
        Status = BackupStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedTime = DateTime.UtcNow;
        
        if (ActualStartTime.HasValue)
        {
            Duration = CompletedTime.Value - ActualStartTime.Value;
        }
    }

    public void Cancel()
    {
        if (Status == BackupStatus.Completed || Status == BackupStatus.Failed)
            throw new InvalidOperationException($"Cannot cancel job in {Status} status");

        Status = BackupStatus.Cancelled;
        CompletedTime = DateTime.UtcNow;
    }

    public void IncrementRetry()
    {
        RetryCount = (RetryCount ?? 0) + 1;
        Status = BackupStatus.Pending;
        ErrorMessage = null;
    }

    public bool CanRetry(int maxRetries)
    {
        return Status == BackupStatus.Failed && (RetryCount ?? 0) < maxRetries;
    }
}
