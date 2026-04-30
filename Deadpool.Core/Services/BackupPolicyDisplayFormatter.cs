using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

public class BackupPolicyDisplayFormatter : IBackupPolicyDisplayFormatter
{
    private readonly ICronScheduleDescriptionService _cronDescriptionService;

    public BackupPolicyDisplayFormatter(ICronScheduleDescriptionService cronDescriptionService)
    {
        _cronDescriptionService = cronDescriptionService ?? throw new ArgumentNullException(nameof(cronDescriptionService));
    }

    public BackupPolicyDisplaySummary Format(
        string fullBackupCron,
        string differentialBackupCron,
        string transactionLogBackupCron,
        string recoveryModel,
        int retentionDays,
        bool? bootstrapFullBackupEnabled = null)
    {
        var fullDescription = _cronDescriptionService.Describe(fullBackupCron);
        var differentialDescription = _cronDescriptionService.Describe(differentialBackupCron);
        var logDescription = _cronDescriptionService.Describe(transactionLogBackupCron);

        return new BackupPolicyDisplaySummary(
            FullBackupSchedule: $"Full Backup runs {fullDescription}",
            DifferentialBackupSchedule: $"Differential Backup runs {differentialDescription}",
            TransactionLogBackupSchedule: $"Transaction Log Backup runs {logDescription}",
            RecoveryModel: $"Recovery Model: {recoveryModel}",
            Retention: $"Retention: {retentionDays} days",
            BootstrapFullBackupEnabled: bootstrapFullBackupEnabled.HasValue
                ? $"Bootstrap Full Backup Enabled: {(bootstrapFullBackupEnabled.Value ? "Yes" : "No")}"
                : null);
    }
}
