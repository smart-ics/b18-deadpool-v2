using Deadpool.Core.Services;
using FluentAssertions;

namespace Deadpool.Tests.Services;

public class BackupPolicyDisplayFormatterTests
{
    private readonly BackupPolicyDisplayFormatter _formatter =
        new(new CronScheduleDescriptionService());

    [Fact]
    public void Format_ShouldReturnOperatorFriendlySummary_ForSupportedSchedules()
    {
        // Act
        var summary = _formatter.Format(
            fullBackupCron: "0 0 * * 0",
            differentialBackupCron: "0 0 * * 1-6",
            transactionLogBackupCron: "*/15 * * * *",
            recoveryModel: "Full",
            retentionDays: 14,
            bootstrapFullBackupEnabled: true);

        // Assert
        summary.FullBackupSchedule.Should().Be("Full Backup runs at midnight every Sunday");
        summary.DifferentialBackupSchedule.Should().Be("Differential Backup runs at midnight Monday through Saturday");
        summary.TransactionLogBackupSchedule.Should().Be("Transaction Log Backup runs every 15 minutes");
        summary.RecoveryModel.Should().Be("Recovery Model: Full");
        summary.Retention.Should().Be("Retention: 14 days");
        summary.BootstrapFullBackupEnabled.Should().Be("Bootstrap Full Backup Enabled: Yes");
    }

    [Fact]
    public void Format_ShouldUseFallback_WhenCronPatternIsUnsupported()
    {
        // Act
        var summary = _formatter.Format(
            fullBackupCron: "0 0 1 * *",
            differentialBackupCron: "0 0 * * 1-6",
            transactionLogBackupCron: "*/15 * * * *",
            recoveryModel: "Full",
            retentionDays: 14);

        // Assert
        summary.FullBackupSchedule.Should().Be("Full Backup runs on a custom schedule");
    }

    [Fact]
    public void Format_ShouldOmitBootstrapLine_WhenBootstrapFlagNotProvided()
    {
        // Act
        var summary = _formatter.Format(
            fullBackupCron: "0 0 * * 0",
            differentialBackupCron: "0 0 * * 1-6",
            transactionLogBackupCron: "*/15 * * * *",
            recoveryModel: "Full",
            retentionDays: 14,
            bootstrapFullBackupEnabled: null);

        // Assert
        summary.BootstrapFullBackupEnabled.Should().BeNull();
    }
}
