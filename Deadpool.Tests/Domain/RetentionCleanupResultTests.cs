using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Domain;

public class RetentionCleanupResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var result = new RetentionCleanupResult("TestDB", isDryRun: false);

        result.DatabaseName.Should().Be("TestDB");
        result.IsDryRun.Should().BeFalse();
        result.CleanupTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.EvaluatedCount.Should().Be(0);
        result.DeletedCount.Should().Be(0);
        result.RetainedCount.Should().Be(0);
        result.FailedDeletionCount.Should().Be(0);
        result.HasFailures.Should().BeFalse();
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDatabaseNameEmpty()
    {
        var act = () => new RetentionCleanupResult("", isDryRun: false);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Database name cannot be empty*");
    }

    [Fact]
    public void AddEvaluatedBackup_ShouldIncrementCount()
    {
        var result = new RetentionCleanupResult("TestDB");
        var backup = CreateTestBackup(BackupType.Full);

        result.AddEvaluatedBackup(backup);

        result.EvaluatedCount.Should().Be(1);
        result.EvaluatedBackups.Should().Contain(backup);
    }

    [Fact]
    public void RecordDeletion_ShouldIncrementDeletedCount()
    {
        var result = new RetentionCleanupResult("TestDB");
        var backup = CreateTestBackup(BackupType.Full);

        result.RecordDeletion(backup);

        result.DeletedCount.Should().Be(1);
        result.DeletedBackups.Should().Contain(backup);
    }

    [Fact]
    public void RecordRetention_ShouldIncrementRetainedCount()
    {
        var result = new RetentionCleanupResult("TestDB");
        var backup = CreateTestBackup(BackupType.Full);

        result.RecordRetention(backup, "Latest Full backup");

        result.RetainedCount.Should().Be(1);
        result.RetainedBackups.Should().Contain(backup);
        result.SafetyReasons.Should().ContainSingle()
            .Which.Should().Contain("Latest Full backup");
    }

    [Fact]
    public void RecordDeletionFailure_ShouldIncrementFailedCount()
    {
        var result = new RetentionCleanupResult("TestDB");
        var backup = CreateTestBackup(BackupType.Full);

        result.RecordDeletionFailure(backup, "Access denied");

        result.FailedDeletionCount.Should().Be(1);
        result.HasFailures.Should().BeTrue();
        result.IsSuccessful.Should().BeFalse();
        result.DeletionFailures.Should().ContainSingle()
            .Which.Should().Contain("Access denied");
    }

    [Fact]
    public void GetSummary_ShouldIncludeAllCounts()
    {
        var result = new RetentionCleanupResult("TestDB", isDryRun: false);
        var backup1 = CreateTestBackup(BackupType.Full);
        var backup2 = CreateTestBackup(BackupType.Differential);

        result.AddEvaluatedBackup(backup1);
        result.AddEvaluatedBackup(backup2);
        result.RecordDeletion(backup1);
        result.RecordRetention(backup2, "Required for chain");

        var summary = result.GetSummary();

        summary.Should().Contain("TestDB");
        summary.Should().Contain("ACTUAL");
        summary.Should().Contain("Evaluated=2");
        summary.Should().Contain("Deleted=1");
        summary.Should().Contain("Retained=1");
        summary.Should().Contain("Failed=0");
    }

    [Fact]
    public void GetSummary_ShouldIndicateDryRun()
    {
        var result = new RetentionCleanupResult("TestDB", isDryRun: true);

        var summary = result.GetSummary();

        summary.Should().Contain("DRY RUN");
    }

    [Fact]
    public void RecordRetention_ShouldThrow_WhenReasonEmpty()
    {
        var result = new RetentionCleanupResult("TestDB");
        var backup = CreateTestBackup(BackupType.Full);

        var act = () => result.RecordRetention(backup, "");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Retention reason cannot be empty*");
    }

    [Fact]
    public void RecordDeletionFailure_ShouldThrow_WhenErrorMessageEmpty()
    {
        var result = new RetentionCleanupResult("TestDB");
        var backup = CreateTestBackup(BackupType.Full);

        var act = () => result.RecordDeletionFailure(backup, "");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Error message cannot be empty*");
    }

    private static BackupJob CreateTestBackup(BackupType backupType)
    {
        return new BackupJob(
            databaseName: "TestDB",
            backupType: backupType,
            backupFilePath: $"C:\\Backups\\test_{backupType}.bak");
    }
}
