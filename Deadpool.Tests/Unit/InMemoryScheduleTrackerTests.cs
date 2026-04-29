using Deadpool.Core.Domain.Enums;
using Deadpool.Infrastructure.Scheduling;
using FluentAssertions;

namespace Deadpool.Tests.Unit;

public class InMemoryScheduleTrackerTests
{
    // ── GetLastScheduled ─────────────────────────────────────────────────────────

    [Fact]
    public void GetLastScheduled_ShouldReturnMinValue_WhenNeverSeeded()
    {
        var tracker = new InMemoryScheduleTracker();

        var result = tracker.GetLastScheduled("MyDB", BackupType.Full);

        result.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void GetLastScheduled_ShouldReturnStoredTime_AfterMarkScheduled()
    {
        var tracker = new InMemoryScheduleTracker();
        var t = new DateTime(2024, 3, 10, 2, 0, 0, DateTimeKind.Utc);

        tracker.MarkScheduled("MyDB", BackupType.Full, t);

        tracker.GetLastScheduled("MyDB", BackupType.Full).Should().Be(t);
    }

    // ── Isolation ────────────────────────────────────────────────────────────────

    [Fact]
    public void Tracker_ShouldIsolateByDatabase()
    {
        var tracker = new InMemoryScheduleTracker();
        var t1 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        tracker.MarkScheduled("DB1", BackupType.Full, t1);
        tracker.MarkScheduled("DB2", BackupType.Full, t2);

        tracker.GetLastScheduled("DB1", BackupType.Full).Should().Be(t1);
        tracker.GetLastScheduled("DB2", BackupType.Full).Should().Be(t2);
    }

    [Fact]
    public void Tracker_ShouldIsolateByBackupType()
    {
        var tracker = new InMemoryScheduleTracker();
        var tFull  = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tDiff  = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var tLog   = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc);

        tracker.MarkScheduled("MyDB", BackupType.Full, tFull);
        tracker.MarkScheduled("MyDB", BackupType.Differential, tDiff);
        tracker.MarkScheduled("MyDB", BackupType.TransactionLog, tLog);

        tracker.GetLastScheduled("MyDB", BackupType.Full).Should().Be(tFull);
        tracker.GetLastScheduled("MyDB", BackupType.Differential).Should().Be(tDiff);
        tracker.GetLastScheduled("MyDB", BackupType.TransactionLog).Should().Be(tLog);
    }

    [Fact]
    public void MarkScheduled_ShouldOverwritePreviousValue()
    {
        var tracker = new InMemoryScheduleTracker();
        var first  = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2024, 1, 8, 0, 0, 0, DateTimeKind.Utc);

        tracker.MarkScheduled("MyDB", BackupType.Full, first);
        tracker.MarkScheduled("MyDB", BackupType.Full, second);

        tracker.GetLastScheduled("MyDB", BackupType.Full).Should().Be(second);
    }

    // ── Thread safety ────────────────────────────────────────────────────────────

    [Fact]
    public void Tracker_ShouldBeThreadSafe()
    {
        var tracker = new InMemoryScheduleTracker();
        var base_ = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            tracker.MarkScheduled("MyDB", BackupType.Full, base_.AddSeconds(i)))).ToArray();

        Task.WaitAll(tasks);

        // No assertion on exact value — just that it doesn't throw and stores something.
        tracker.GetLastScheduled("MyDB", BackupType.Full).Should().BeOnOrAfter(base_);
    }
}
