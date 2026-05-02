using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Services;
using FluentAssertions;

namespace Deadpool.Tests.Services;

public class RestoreScriptBuilderServiceTests
{
    private readonly RestoreScriptBuilderService _service = new();

    [Fact]
    public void Build_FullOnly_GeneratesFullThenDatabaseRecovery()
    {
        var full = CreateCompletedFullBackup(@"C:\Backups\full.bak", DateTime.UtcNow.AddHours(-2));
        var plan = RestorePlan.CreateValidPlan(
            "TestDB",
            full.EndTime!.Value,
            full,
            differentialBackup: null,
            logBackups: Array.Empty<BackupJob>(),
            actualRestorePoint: full.EndTime.Value);

        var script = _service.Build(plan);

        script.Commands.Should().HaveCount(2);
        script.Commands[0].Should().Be("RESTORE DATABASE [TestDB] FROM DISK = 'C:\\Backups\\full.bak' WITH NORECOVERY;");
        script.Commands[1].Should().Be("RESTORE DATABASE [TestDB] WITH RECOVERY;");
        script.ToSql().Should().Contain("RESTORE DATABASE [TestDB] FROM DISK");
    }

    [Fact]
    public void Build_FullAndDifferentialOnly_GeneratesRecoveryAfterDifferential()
    {
        var full = CreateCompletedFullBackup(@"C:\Backups\full.bak", DateTime.UtcNow.AddHours(-4));
        var diff = CreateCompletedDifferentialBackup(@"C:\Backups\diff.bak", DateTime.UtcNow.AddHours(-2), full.CheckpointLSN!.Value);

        var plan = RestorePlan.CreateValidPlan(
            "TestDB",
            diff.EndTime!.Value,
            full,
            diff,
            logBackups: Array.Empty<BackupJob>(),
            actualRestorePoint: diff.EndTime.Value);

        var script = _service.Build(plan);

        script.Commands.Should().HaveCount(3);
        script.Commands[0].Should().Be("RESTORE DATABASE [TestDB] FROM DISK = 'C:\\Backups\\full.bak' WITH NORECOVERY;");
        script.Commands[1].Should().Be("RESTORE DATABASE [TestDB] FROM DISK = 'C:\\Backups\\diff.bak' WITH NORECOVERY;");
        script.Commands[2].Should().Be("RESTORE DATABASE [TestDB] WITH RECOVERY;");
    }

    [Fact]
    public void Build_FullAndLogs_GeneratesFinalLogWithStopAtRecoveryOnly()
    {
        var targetTime = DateTime.Parse("2026-05-02T14:35:00Z").ToUniversalTime();

        var full = CreateCompletedFullBackup(@"C:\Backups\full.bak", targetTime.AddHours(-4));
        var log1 = CreateCompletedLogBackup(@"C:\Backups\log1.trn", targetTime.AddHours(-2), full.LastLSN!.Value);
        var log2 = CreateCompletedLogBackup(@"C:\Backups\log2.trn", targetTime.AddHours(-1), log1.LastLSN!.Value);

        var plan = RestorePlan.CreateValidPlan(
            "TestDB",
            targetTime,
            full,
            differentialBackup: null,
            logBackups: new List<BackupJob> { log1, log2 },
            actualRestorePoint: targetTime);

        var script = _service.Build(plan);

        script.Commands.Should().HaveCount(3);
        script.Commands[0].Should().Be("RESTORE DATABASE [TestDB] FROM DISK = 'C:\\Backups\\full.bak' WITH NORECOVERY;");
        script.Commands[1].Should().Be("RESTORE LOG [TestDB] FROM DISK = 'C:\\Backups\\log1.trn' WITH NORECOVERY;");
        script.Commands[2].Should().Be("RESTORE LOG [TestDB] FROM DISK = 'C:\\Backups\\log2.trn' WITH STOPAT = '2026-05-02T14:35:00', RECOVERY;");

        script.Commands[1].Should().NotContain("STOPAT");
        script.Commands[1].Should().Contain("WITH NORECOVERY;");
    }

    [Fact]
    public void Build_FullDiffLogs_UsesStrictOrder()
    {
        var targetTime = DateTime.UtcNow;

        var full = CreateCompletedFullBackup(@"C:\Backups\full.bak", targetTime.AddHours(-6));
        var diff = CreateCompletedDifferentialBackup(@"C:\Backups\diff.bak", targetTime.AddHours(-4), full.CheckpointLSN!.Value);
        var log1 = CreateCompletedLogBackup(@"C:\Backups\log1.trn", targetTime.AddHours(-2), diff.LastLSN!.Value);
        var log2 = CreateCompletedLogBackup(@"C:\Backups\log2.trn", targetTime.AddHours(-1), log1.LastLSN!.Value);

        var plan = RestorePlan.CreateValidPlan(
            "TestDB",
            targetTime,
            full,
            diff,
            logBackups: new List<BackupJob> { log1, log2 },
            actualRestorePoint: targetTime);

        var script = _service.Build(plan);

        script.Commands.Should().HaveCount(4);
        script.Commands[0].Should().Contain("RESTORE DATABASE [TestDB] FROM DISK = 'C:\\Backups\\full.bak' WITH NORECOVERY;");
        script.Commands[1].Should().Contain("RESTORE DATABASE [TestDB] FROM DISK = 'C:\\Backups\\diff.bak' WITH NORECOVERY;");
        script.Commands[2].Should().Contain("RESTORE LOG [TestDB] FROM DISK = 'C:\\Backups\\log1.trn' WITH NORECOVERY;");
        script.Commands[3].Should().Contain("RESTORE LOG [TestDB] FROM DISK = 'C:\\Backups\\log2.trn' WITH STOPAT = '");
    }

    [Fact]
    public void Build_InvalidPlan_Throws()
    {
        var plan = RestorePlan.CreateInvalidPlan("TestDB", DateTime.UtcNow, "Invalid chain");

        var act = () => _service.Build(plan);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be valid*");
    }

    [Fact]
    public void Build_EscapesDatabaseAndPathLiterals()
    {
        var full = CreateCompletedFullBackup(@"C:\Backups\fi'le.bak", DateTime.UtcNow.AddHours(-2));
        var plan = RestorePlan.CreateValidPlan(
            "Db]Name",
            full.EndTime!.Value,
            full,
            differentialBackup: null,
            logBackups: Array.Empty<BackupJob>(),
            actualRestorePoint: full.EndTime.Value);

        var script = _service.Build(plan);

        script.Commands[0].Should().Contain("[Db]]Name]");
        script.Commands[0].Should().Contain("fi''le.bak");
    }

    private static BackupJob CreateCompletedFullBackup(string path, DateTime startTime)
    {
        var endTime = startTime.AddMinutes(20);
        var checkpointLsn = 2000m + (startTime.Ticks / TimeSpan.TicksPerHour);

        return BackupJob.Restore(
            "TestDB",
            BackupType.Full,
            BackupStatus.Completed,
            startTime,
            endTime,
            path,
            1024 * 1024 * 100,
            null,
            checkpointLsn - 50m,
            checkpointLsn + 50m,
            null,
            checkpointLsn);
    }

    private static BackupJob CreateCompletedDifferentialBackup(string path, DateTime startTime, decimal baseFullLsn)
    {
        var endTime = startTime.AddMinutes(10);

        return BackupJob.Restore(
            "TestDB",
            BackupType.Differential,
            BackupStatus.Completed,
            startTime,
            endTime,
            path,
            1024 * 1024 * 40,
            null,
            baseFullLsn + 1m,
            baseFullLsn + 20m,
            baseFullLsn,
            null);
    }

    private static BackupJob CreateCompletedLogBackup(string path, DateTime startTime, decimal firstLsn)
    {
        var endTime = startTime.AddMinutes(30);

        return BackupJob.Restore(
            "TestDB",
            BackupType.TransactionLog,
            BackupStatus.Completed,
            startTime,
            endTime,
            path,
            1024 * 1024 * 10,
            null,
            firstLsn,
            firstLsn + 50m,
            null,
            null);
    }
}
