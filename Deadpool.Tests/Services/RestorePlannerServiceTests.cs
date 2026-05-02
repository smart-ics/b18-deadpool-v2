using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;

namespace Deadpool.Tests.Services;

public class RestorePlannerServiceTests
{
    private readonly Mock<IBackupJobRepository> _repository = new();
    private readonly RestorePlannerService _service;

    public RestorePlannerServiceTests()
    {
        _service = new RestorePlannerService(_repository.Object);
    }

    [Fact]
    public async Task BuildRestorePlanAsync_NoFullBeforeTarget_ReturnsInvalidPlan()
    {
        var now = DateTime.UtcNow;
        var log = CreateCompletedLogBackup("Log1.trn", now.AddHours(-2), TimeSpan.FromMinutes(10), 1000m);

        _repository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { log });

        var plan = await _service.BuildRestorePlanAsync("TestDB", now);

        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("No valid Full backup");
    }

    [Fact]
    public async Task BuildRestorePlanAsync_FullOnlyWithExactStopAt_ReturnsValidPlan()
    {
        var full = CreateCompletedFullBackup("Full.bak", DateTime.UtcNow.AddHours(-4), TimeSpan.FromMinutes(20));

        _repository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { full });

        var plan = await _service.BuildRestorePlanAsync("TestDB", full.EndTime!.Value);

        plan.IsValid.Should().BeTrue();
        plan.FullBackup.Should().Be(full);
        plan.DifferentialBackup.Should().BeNull();
        plan.LogBackups.Should().BeEmpty();
        plan.TargetTime.Should().Be(full.EndTime.Value);
    }

    [Fact]
    public async Task BuildRestorePlanAsync_SelectsLatestValidDifferentialBeforeTarget()
    {
        var full = CreateCompletedFullBackup("Full.bak", DateTime.UtcNow.AddHours(-8), TimeSpan.FromMinutes(20));
        var olderDiff = CreateCompletedDifferentialBackup("Diff1.bak", DateTime.UtcNow.AddHours(-6), TimeSpan.FromMinutes(10), full.CheckpointLSN!.Value);
        var latestDiff = CreateCompletedDifferentialBackup("Diff2.bak", DateTime.UtcNow.AddHours(-4), TimeSpan.FromMinutes(10), full.CheckpointLSN!.Value);

        _repository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { full, olderDiff, latestDiff });

        var plan = await _service.BuildRestorePlanAsync("TestDB", latestDiff.EndTime!.Value);

        plan.IsValid.Should().BeTrue();
        plan.DifferentialBackup.Should().Be(latestDiff);
    }

    [Fact]
    public async Task BuildRestorePlanAsync_FullToLogChain_ReturnsOrderedLogsUpToTarget()
    {
        var full = CreateCompletedFullBackup("Full.bak", DateTime.UtcNow.AddHours(-8), TimeSpan.FromMinutes(20));
        var log1 = CreateCompletedLogBackup("Log1.trn", full.EndTime!.Value.AddMinutes(1), TimeSpan.FromMinutes(30), full.LastLSN!.Value);
        var log2 = CreateCompletedLogBackup("Log2.trn", log1.EndTime!.Value.AddMinutes(1), TimeSpan.FromMinutes(30), log1.LastLSN!.Value);

        _repository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { full, log1, log2 });

        var targetTime = log2.StartTime.AddMinutes(10);
        var plan = await _service.BuildRestorePlanAsync("TestDB", targetTime);

        plan.IsValid.Should().BeTrue();
        plan.LogBackups.Should().ContainInOrder(log1, log2);
        plan.TargetTime.Should().Be(targetTime);
    }

    [Fact]
    public async Task BuildRestorePlanAsync_ExactStopAtLogEnd_UsesRequiredLogsOnly()
    {
        var full = CreateCompletedFullBackup("Full.bak", DateTime.UtcNow.AddHours(-8), TimeSpan.FromMinutes(20));
        var log1 = CreateCompletedLogBackup("Log1.trn", full.EndTime!.Value.AddMinutes(1), TimeSpan.FromMinutes(30), full.LastLSN!.Value);
        var log2 = CreateCompletedLogBackup("Log2.trn", log1.EndTime!.Value.AddMinutes(1), TimeSpan.FromMinutes(30), log1.LastLSN!.Value);

        _repository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { full, log1, log2 });

        var plan = await _service.BuildRestorePlanAsync("TestDB", log1.EndTime!.Value);

        plan.IsValid.Should().BeTrue();
        plan.LogBackups.Should().HaveCount(1);
        plan.LogBackups[0].Should().Be(log1);
    }

    [Fact]
    public async Task BuildRestorePlanAsync_BrokenLogChain_ReturnsInvalidPlan()
    {
        var full = CreateCompletedFullBackup("Full.bak", DateTime.UtcNow.AddHours(-8), TimeSpan.FromMinutes(20));
        var log1 = CreateCompletedLogBackup("Log1.trn", full.EndTime!.Value.AddMinutes(1), TimeSpan.FromMinutes(30), full.LastLSN!.Value);
        var log2 = CreateCompletedLogBackup("Log2.trn", log1.EndTime!.Value.AddMinutes(1), TimeSpan.FromMinutes(30), log1.LastLSN!.Value + 500m);

        _repository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { full, log1, log2 });

        var plan = await _service.BuildRestorePlanAsync("TestDB", log2.StartTime.AddMinutes(5));

        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("Broken transaction log chain");
    }

    [Fact]
    public async Task BuildRestorePlanAsync_TargetBeyondLogCoverage_ReturnsInvalidPlan()
    {
        var full = CreateCompletedFullBackup("Full.bak", DateTime.UtcNow.AddHours(-8), TimeSpan.FromMinutes(20));
        var log1 = CreateCompletedLogBackup("Log1.trn", full.EndTime!.Value.AddMinutes(1), TimeSpan.FromMinutes(30), full.LastLSN!.Value);

        _repository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { full, log1 });

        var plan = await _service.BuildRestorePlanAsync("TestDB", log1.EndTime!.Value.AddHours(1));

        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("beyond available log coverage");
    }

    private static BackupJob CreateCompletedFullBackup(string fileName, DateTime startTime, TimeSpan duration)
    {
        var endTime = startTime.Add(duration);
        var checkpointLsn = 2000m + (startTime.Ticks / TimeSpan.TicksPerHour);

        return BackupJob.Restore(
            "TestDB",
            BackupType.Full,
            BackupStatus.Completed,
            startTime,
            endTime,
            $@"C:\Backups\{fileName}",
            1024 * 1024 * 100,
            null,
            checkpointLsn - 50m,
            checkpointLsn + 50m,
            null,
            checkpointLsn);
    }

    private static BackupJob CreateCompletedDifferentialBackup(string fileName, DateTime startTime, TimeSpan duration, decimal baseFullLsn)
    {
        var endTime = startTime.Add(duration);

        return BackupJob.Restore(
            "TestDB",
            BackupType.Differential,
            BackupStatus.Completed,
            startTime,
            endTime,
            $@"C:\Backups\{fileName}",
            1024 * 1024 * 40,
            null,
            baseFullLsn + 1m,
            baseFullLsn + 20m,
            baseFullLsn,
            null);
    }

    private static BackupJob CreateCompletedLogBackup(string fileName, DateTime startTime, TimeSpan duration, decimal firstLsn)
    {
        var endTime = startTime.Add(duration);

        return BackupJob.Restore(
            "TestDB",
            BackupType.TransactionLog,
            BackupStatus.Completed,
            startTime,
            endTime,
            $@"C:\Backups\{fileName}",
            1024 * 1024 * 10,
            null,
            firstLsn,
            firstLsn + 50m,
            null,
            null);
    }
}
