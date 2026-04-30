namespace Deadpool.Core.Domain.ValueObjects;

public record BackupPolicyDisplaySummary(
    string FullBackupSchedule,
    string DifferentialBackupSchedule,
    string TransactionLogBackupSchedule,
    string RecoveryModel,
    string Retention,
    string? BootstrapFullBackupEnabled);
