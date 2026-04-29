using Deadpool.Agent.Configuration;
using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Deadpool.Tests.Unit;

public class BackupSchedulerWorkerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    // Full at noon daily; Diff and Log never fire (far-future cron).
    private static BackupSchedulerWorker BuildWorker(
        Mock<IBackupJobRepository> repoMock,
        IScheduleTracker? tracker = null,
        List<DatabaseBackupPolicyOptions>? policies = null)
    {
        policies ??= new List<DatabaseBackupPolicyOptions>
        {
            new()
            {
                DatabaseName = "TestDB",
                FullBackupCron        = "0 12 * * *",  // daily at noon
                DifferentialBackupCron = "0 1 * * *",  // daily at 01:00
                TransactionLogBackupCron = "*/15 * * * *" // every 15 min
            }
        };

        tracker ??= new InMemoryScheduleTracker();

        return new BackupSchedulerWorker(
            NullLogger<BackupSchedulerWorker>.Instance,
            repoMock.Object,
            tracker,
            Options.Create(policies));
    }

    private static Mock<IBackupJobRepository> EmptyRepo()
    {
        var m = new Mock<IBackupJobRepository>();
        m.Setup(r => r.GetRecentJobsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Enumerable.Empty<BackupJob>());
        m.Setup(r => r.CreateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    // ── Construction ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldThrow_WhenPoliciesIsNull()
    {
        var act = () => new BackupSchedulerWorker(
            NullLogger<BackupSchedulerWorker>.Instance,
            EmptyRepo().Object,
            new InMemoryScheduleTracker(),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Tick — job creation ───────────────────────────────────────────────────────

    [Fact]
    public async Task Tick_ShouldCreateFullBackupJob_WhenDue()
    {
        var repo = EmptyRepo();
        var tracker = new InMemoryScheduleTracker();
        var worker = BuildWorker(repo, tracker);

        // lastScheduled = MinValue, now = just after noon on 2024-01-01
        var now = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc);

        await worker.TickAsync(now, CancellationToken.None);

        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j =>
                j.DatabaseName == "TestDB" &&
                j.BackupType == BackupType.Full &&
                j.Status == BackupStatus.Pending)),
            Times.Once);
    }

    [Fact]
    public async Task Tick_ShouldNotCreateJob_WhenNotDue()
    {
        var repo = EmptyRepo();
        var tracker = new InMemoryScheduleTracker();
        // Seed tracker so the last full backup was just fired at noon today
        tracker.MarkScheduled("TestDB", BackupType.Full,
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var worker = BuildWorker(repo, tracker);
        // Now = 13:00, same day — next occurrence is tomorrow noon
        var now = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc);

        await worker.TickAsync(now, CancellationToken.None);

        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j => j.BackupType == BackupType.Full)),
            Times.Never);
    }

    // ── Duplicate prevention ─────────────────────────────────────────────────────

    [Fact]
    public async Task Tick_ShouldNotCreateDuplicateJob_WhenCalledTwiceForSameTick()
    {
        var repo = EmptyRepo();
        var tracker = new InMemoryScheduleTracker();
        var worker = BuildWorker(repo, tracker);
        var now = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc);

        await worker.TickAsync(now, CancellationToken.None);
        await worker.TickAsync(now, CancellationToken.None); // simulates double-fire

        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j => j.BackupType == BackupType.Full)),
            Times.Once, "second tick should be suppressed by tracker");
    }

    [Fact]
    public async Task Tick_ShouldScheduleJobOnce_WhenPolledFrequentlyBetweenOccurrences()
    {
        var repo = EmptyRepo();
        var tracker = new InMemoryScheduleTracker();
        var worker = BuildWorker(repo, tracker);

        // Simulate three poll ticks all after the noon occurrence but before the next
        var times = new[]
        {
            new DateTime(2024, 1, 1, 12,  1, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 12, 30, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 13,  0, 0, DateTimeKind.Utc),
        };

        foreach (var t in times)
            await worker.TickAsync(t, CancellationToken.None);

        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j => j.BackupType == BackupType.Full)),
            Times.Once);
    }

    // ── Restart safety ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Tick_ShouldNotFireAgain_WhenTrackerRestoredFromRepository()
    {
        // Simulate: scheduler restarted, repository has a recent Full job created today
        // at noon. Tracker is seeded from repository before first tick.
        var existingJob = new BackupJob("TestDB", BackupType.Full, "some_path.bak");

        var repo = new Mock<IBackupJobRepository>();
        repo.Setup(r => r.GetRecentJobsAsync("TestDB", 1))
            .ReturnsAsync(new[] { existingJob });
        repo.Setup(r => r.CreateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);

        var tracker = new InMemoryScheduleTracker();
        // Seed the tracker exactly as SeedTrackerFromRepositoryAsync would
        tracker.MarkScheduled("TestDB", BackupType.Full, existingJob.StartTime);
        tracker.MarkScheduled("TestDB", BackupType.Differential, existingJob.StartTime);
        tracker.MarkScheduled("TestDB", BackupType.TransactionLog, existingJob.StartTime);

        var worker = BuildWorker(repo, tracker);

        // Now tick at a time where noon has already fired (StartTime was just set).
        // StartTime = DateTime.UtcNow inside the entity so the tracker is current.
        var now = existingJob.StartTime.AddMinutes(5);

        await worker.TickAsync(now, CancellationToken.None);

        // Full job should NOT be created again because tracker already records it.
        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j => j.BackupType == BackupType.Full)),
            Times.Never);
    }

    // ── Per-database isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task Tick_ShouldScheduleJobsForAllDatabases()
    {
        var repo = EmptyRepo();
        var tracker = new InMemoryScheduleTracker();

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new() { DatabaseName = "DB1", FullBackupCron = "0 12 * * *",
                    DifferentialBackupCron = "0 1 * * *",
                    TransactionLogBackupCron = "*/15 * * * *" },
            new() { DatabaseName = "DB2", FullBackupCron = "0 12 * * *",
                    DifferentialBackupCron = "0 1 * * *",
                    TransactionLogBackupCron = "*/15 * * * *" }
        };

        var worker = BuildWorker(repo, tracker, policies);
        var now = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc);

        await worker.TickAsync(now, CancellationToken.None);

        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j => j.DatabaseName == "DB1" && j.BackupType == BackupType.Full)),
            Times.Once);

        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j => j.DatabaseName == "DB2" && j.BackupType == BackupType.Full)),
            Times.Once);
    }

    [Fact]
    public async Task Tick_ShouldContinueOtherDatabases_WhenOneRepositoryCallFails()
    {
        var repo = new Mock<IBackupJobRepository>();

        var callCount = 0;
        repo.Setup(r => r.GetRecentJobsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Enumerable.Empty<BackupJob>());

        repo.Setup(r => r.CreateAsync(It.IsAny<BackupJob>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Simulated DB1 failure");
                return Task.CompletedTask;
            });

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new() { DatabaseName = "DB1", FullBackupCron = "0 12 * * *",
                    DifferentialBackupCron = "0 1 * * *", TransactionLogBackupCron = "*/15 * * * *" },
            new() { DatabaseName = "DB2", FullBackupCron = "0 12 * * *",
                    DifferentialBackupCron = "0 1 * * *", TransactionLogBackupCron = "*/15 * * * *" }
        };

        var worker = BuildWorker(repo, new InMemoryScheduleTracker(), policies);
        var now = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc);

        // Should NOT throw even though DB1 full backup fails.
        var act = async () => await worker.TickAsync(now, CancellationToken.None);
        await act.Should().NotThrowAsync();

        // DB2 full backup should still be attempted.
        repo.Verify(r => r.CreateAsync(
            It.Is<BackupJob>(j => j.DatabaseName == "DB2" && j.BackupType == BackupType.Full)),
            Times.Once);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tick_ShouldStopEarly_WhenCancellationRequested()
    {
        var repo = EmptyRepo();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new() { DatabaseName = "DB1", FullBackupCron = "0 12 * * *",
                    DifferentialBackupCron = "0 1 * * *", TransactionLogBackupCron = "*/15 * * * *" },
            new() { DatabaseName = "DB2", FullBackupCron = "0 12 * * *",
                    DifferentialBackupCron = "0 1 * * *", TransactionLogBackupCron = "*/15 * * * *" }
        };

        var worker = BuildWorker(repo, policies: policies);
        var now = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc);

        await worker.TickAsync(now, cts.Token);

        // Nothing should have been created because cancellation stops the loop
        // before any database is processed.
        repo.Verify(r => r.CreateAsync(It.IsAny<BackupJob>()), Times.Never);
    }

    // ── Placeholder path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Tick_ShouldSetPendingPlaceholderPath_WithCorrectExtension()
    {
        BackupJob? captured = null;
        var repo = EmptyRepo();
        repo.Setup(r => r.CreateAsync(It.IsAny<BackupJob>()))
            .Callback<BackupJob>(j => captured = j)
            .Returns(Task.CompletedTask);

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new() { DatabaseName = "TestDB",
                    FullBackupCron = "0 12 * * *",
                    DifferentialBackupCron = "0 1 * * *",
                    TransactionLogBackupCron = "*/15 * * * *" }
        };

        var worker = BuildWorker(repo, policies: policies);
        // Log schedule at 12:00 — but we want the log backup: tick at 12:15
        var tracker = new InMemoryScheduleTracker();
        // Seed full and diff as already done so only log fires
        tracker.MarkScheduled("TestDB", BackupType.Full,
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        tracker.MarkScheduled("TestDB", BackupType.Differential,
            new DateTime(2024, 1, 1,  1, 0, 0, DateTimeKind.Utc));

        var worker2 = BuildWorker(repo, tracker, policies);
        var now = new DateTime(2024, 1, 1, 12, 16, 0, DateTimeKind.Utc);

        await worker2.TickAsync(now, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.BackupFilePath.Should().StartWith("PENDING_");
        captured.BackupFilePath.Should().EndWith(".trn");
        captured.Status.Should().Be(BackupStatus.Pending);
    }
}
