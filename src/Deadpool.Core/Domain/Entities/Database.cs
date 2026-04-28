using Deadpool.Core.Domain.Common;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

/// <summary>
/// Represents a database on a SQL Server instance
/// </summary>
public class Database : Entity
{
    public Guid SqlServerInstanceId { get; private set; }
    public string Name { get; private set; }
    public RecoveryModel RecoveryModel { get; private set; }
    public bool IsSystemDatabase { get; private set; }
    public long SizeInBytes { get; private set; }
    public DateTime? LastBackupDate { get; private set; }
    public DateTime? LastLogBackupDate { get; private set; }
    public bool IsOnline { get; private set; }
    public string? Collation { get; private set; }
    public int CompatibilityLevel { get; private set; }

    private readonly List<BackupJob> _backupJobs = new();
    public IReadOnlyCollection<BackupJob> BackupJobs => _backupJobs.AsReadOnly();

    private Database() : base()
    {
        Name = string.Empty;
    }

    public Database(
        Guid sqlServerInstanceId,
        string name,
        RecoveryModel recoveryModel,
        bool isSystemDatabase = false) : base()
    {
        SqlServerInstanceId = sqlServerInstanceId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        RecoveryModel = recoveryModel;
        IsSystemDatabase = isSystemDatabase;
        IsOnline = true;
    }

    public void UpdateRecoveryModel(RecoveryModel recoveryModel)
    {
        RecoveryModel = recoveryModel;
    }

    public void UpdateSize(long sizeInBytes)
    {
        if (sizeInBytes < 0)
            throw new ArgumentException("Size cannot be negative", nameof(sizeInBytes));
        
        SizeInBytes = sizeInBytes;
    }

    public void UpdateLastBackupDate(DateTime backupDate, BackupType backupType)
    {
        if (backupType == BackupType.Log)
        {
            LastLogBackupDate = backupDate;
        }
        else
        {
            LastBackupDate = backupDate;
        }
    }

    public void SetOnlineStatus(bool isOnline)
    {
        IsOnline = isOnline;
    }

    public void UpdateDatabaseInfo(string? collation, int compatibilityLevel)
    {
        Collation = collation;
        CompatibilityLevel = compatibilityLevel;
    }

    public bool RequiresBackup(TimeSpan threshold)
    {
        if (!LastBackupDate.HasValue)
            return true;

        return DateTime.UtcNow - LastBackupDate.Value > threshold;
    }

    public bool SupportsLogBackups()
    {
        return RecoveryModel == RecoveryModel.Full || RecoveryModel == RecoveryModel.BulkLogged;
    }
}
