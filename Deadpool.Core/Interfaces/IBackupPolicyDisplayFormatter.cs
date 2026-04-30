using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

public interface IBackupPolicyDisplayFormatter
{
    BackupPolicyDisplaySummary Format(
        string fullBackupCron,
        string differentialBackupCron,
        string transactionLogBackupCron,
        string recoveryModel,
        int retentionDays,
        bool? bootstrapFullBackupEnabled = null);
}
