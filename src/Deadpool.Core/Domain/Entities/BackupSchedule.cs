using Deadpool.Core.Domain.Common;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

/// <summary>
/// Represents a recurring backup schedule configuration
/// </summary>
public class BackupSchedule : Entity
{
    public Guid DatabaseId { get; private set; }
    public string Name { get; private set; }
    public BackupType BackupType { get; private set; }
    public bool IsEnabled { get; private set; }
    public string CronExpression { get; private set; }
    public string BackupPathTemplate { get; private set; }
    public int RetentionDays { get; private set; }
    public bool IsCompressed { get; private set; }
    public bool IsEncrypted { get; private set; }
    public int MaxRetryAttempts { get; private set; }
    public DateTime? NextRunTime { get; private set; }
    public DateTime? LastRunTime { get; private set; }
    public Guid? LastBackupJobId { get; private set; }
    public string? Description { get; private set; }

    private BackupSchedule() : base()
    {
        Name = string.Empty;
        CronExpression = string.Empty;
        BackupPathTemplate = string.Empty;
    }

    public BackupSchedule(
        Guid databaseId,
        string name,
        BackupType backupType,
        string cronExpression,
        string backupPathTemplate,
        int retentionDays = 7,
        bool isCompressed = true,
        bool isEncrypted = false,
        int maxRetryAttempts = 3) : base()
    {
        DatabaseId = databaseId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        BackupType = backupType;
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
        BackupPathTemplate = backupPathTemplate ?? throw new ArgumentNullException(nameof(backupPathTemplate));
        RetentionDays = retentionDays > 0 ? retentionDays : throw new ArgumentException("Retention days must be positive");
        IsCompressed = isCompressed;
        IsEncrypted = isEncrypted;
        MaxRetryAttempts = maxRetryAttempts >= 0 ? maxRetryAttempts : throw new ArgumentException("Max retry attempts cannot be negative");
        IsEnabled = true;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

    public void UpdateSchedule(string cronExpression, DateTime? nextRunTime = null)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
        NextRunTime = nextRunTime;
    }

    public void UpdateRetention(int retentionDays)
    {
        if (retentionDays <= 0)
            throw new ArgumentException("Retention days must be positive");
        
        RetentionDays = retentionDays;
    }

    public void UpdateBackupSettings(bool isCompressed, bool isEncrypted)
    {
        IsCompressed = isCompressed;
        IsEncrypted = isEncrypted;
    }

    public void RecordExecution(Guid backupJobId, DateTime executionTime, DateTime? nextRun)
    {
        LastBackupJobId = backupJobId;
        LastRunTime = executionTime;
        NextRunTime = nextRun;
    }

    public string GenerateBackupFilePath(string databaseName, DateTime timestamp)
    {
        var path = BackupPathTemplate
            .Replace("{database}", databaseName)
            .Replace("{type}", BackupType.ToString())
            .Replace("{timestamp}", timestamp.ToString("yyyyMMdd_HHmmss"))
            .Replace("{date}", timestamp.ToString("yyyyMMdd"));

        return path;
    }
}
