using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Domain.Entities;

public class BackupPolicy
{
    public string DatabaseName { get; }
    public RecoveryModel RecoveryModel { get; }
    public BackupSchedule FullBackupSchedule { get; }
    public BackupSchedule DifferentialBackupSchedule { get; }
    public BackupSchedule? TransactionLogBackupSchedule { get; }
    public int RetentionDays { get; }

    public BackupPolicy(
        string databaseName,
        RecoveryModel recoveryModel,
        BackupSchedule fullBackupSchedule,
        BackupSchedule differentialBackupSchedule,
        BackupSchedule? transactionLogBackupSchedule,
        int retentionDays)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (retentionDays <= 0)
            throw new ArgumentException("Retention days must be greater than zero.", nameof(retentionDays));

        DatabaseName = databaseName;
        RecoveryModel = recoveryModel;
        FullBackupSchedule = fullBackupSchedule ?? throw new ArgumentNullException(nameof(fullBackupSchedule));
        DifferentialBackupSchedule = differentialBackupSchedule ?? throw new ArgumentNullException(nameof(differentialBackupSchedule));

        ValidateTransactionLogSchedule(recoveryModel, transactionLogBackupSchedule);

        TransactionLogBackupSchedule = transactionLogBackupSchedule;
        RetentionDays = retentionDays;
    }

    public bool CanDeleteBackup(BackupType backupType, DateTime backupDate, DateTime? lastFullBackupDate)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);

        if (backupDate > cutoffDate)
            return false;

        if (!lastFullBackupDate.HasValue)
            return false;

        if (backupType == BackupType.Full && backupDate >= lastFullBackupDate.Value)
            return false;

        return backupDate < lastFullBackupDate.Value;
    }

    public bool SupportsTransactionLogBackup()
    {
        return RecoveryModel == RecoveryModel.Full || RecoveryModel == RecoveryModel.BulkLogged;
    }

    private void ValidateTransactionLogSchedule(RecoveryModel recoveryModel, BackupSchedule? transactionLogBackupSchedule)
    {
        var supportsLogBackup = recoveryModel == RecoveryModel.Full || recoveryModel == RecoveryModel.BulkLogged;

        if (!supportsLogBackup && transactionLogBackupSchedule != null)
            throw new InvalidOperationException(
                $"Transaction log backup is not supported for recovery model '{recoveryModel}'. Use Full or BulkLogged recovery model.");

        if (supportsLogBackup && transactionLogBackupSchedule == null)
            throw new ArgumentNullException(
                nameof(transactionLogBackupSchedule),
                $"Transaction log backup schedule is required for recovery model '{recoveryModel}'.");
    }
}
