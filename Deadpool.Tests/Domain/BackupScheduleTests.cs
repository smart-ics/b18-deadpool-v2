using Cronos;
using Deadpool.Core.Domain.ValueObjects;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class BackupScheduleTests
{
    // ── Construction ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldCreateInstance_WhenValidCronExpression()
    {
        var schedule = new BackupSchedule("0 2 * * 0");

        schedule.CronExpression.Should().Be("0 2 * * 0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenCronExpressionEmpty(string cronExpression)
    {
        var act = () => new BackupSchedule(cronExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Cron expression cannot be empty.*");
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("99 99 99 99 99")]
    [InlineData("* * * * * *")]        // 6-part not supported with Standard format
    public void Constructor_ShouldThrow_WhenCronExpressionInvalid(string cronExpression)
    {
        var act = () => new BackupSchedule(cronExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid cron expression*");
    }

    [Fact]
    public void BackupSchedule_ShouldSupportRecordEquality()
    {
        var s1 = new BackupSchedule("0 2 * * 0");
        var s2 = new BackupSchedule("0 2 * * 0");
        var s3 = new BackupSchedule("0 3 * * *");

        s1.Should().Be(s2);
        s1.Should().NotBe(s3);
    }

    // ── GetNextOccurrence ────────────────────────────────────────────────────────

    [Fact]
    public void GetNextOccurrence_ShouldReturnNextScheduledTime()
    {
        // Daily at noon (local time)
        var schedule = new BackupSchedule("0 12 * * *");
        var from = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Local);

        var next = schedule.GetNextOccurrence(from);

        next.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local));
    }

    [Fact]
    public void GetNextOccurrence_ShouldReturnTomorrow_WhenAlreadyPastToday()
    {
        // Daily at noon (local time)
        var schedule = new BackupSchedule("0 12 * * *");
        var from = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Local); // already past noon

        var next = schedule.GetNextOccurrence(from);

        next.Should().Be(new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Local));
    }

    [Fact]
    public void GetNextOccurrence_ShouldNotReturnCurrentTime_WhenExactMatch()
    {
        // Cronos GetNextOccurrence is exclusive of the from value.
        var schedule = new BackupSchedule("0 12 * * *");
        var from = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local); // exactly noon

        var next = schedule.GetNextOccurrence(from);

        // The very moment 12:00 is the lower bound; next occurrence is 12:00 tomorrow.
        next.Should().Be(new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Local));
    }

    // ── IsDue ───────────────────────────────────────────────────────────────────

    [Fact]
    public void IsDue_ShouldReturnTrue_WhenOccurrenceFallsInWindow()
    {
        // Runs at :00 and :15 each hour
        var schedule = new BackupSchedule("0,15 * * * *");
        var lastCheck = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);
        var now       = new DateTime(2024, 1, 1, 12, 16, 0, DateTimeKind.Local);

        schedule.IsDue(lastCheck, now).Should().BeTrue();
    }

    [Fact]
    public void IsDue_ShouldReturnFalse_WhenNoOccurrenceInWindow()
    {
        // Daily at midnight
        var schedule = new BackupSchedule("0 0 * * *");
        var lastCheck = new DateTime(2024, 1, 1,  1, 0, 0, DateTimeKind.Local);
        var now       = new DateTime(2024, 1, 1, 23, 0, 0, DateTimeKind.Local);

        schedule.IsDue(lastCheck, now).Should().BeFalse();
    }

    [Fact]
    public void IsDue_ShouldReturnTrue_WhenOccurrenceIsExactlyNow()
    {
        // Daily at noon
        var schedule = new BackupSchedule("0 12 * * *");
        var lastCheck = new DateTime(2024, 1, 1, 11, 59, 0, DateTimeKind.Local);
        var now       = new DateTime(2024, 1, 1, 12,  0, 0, DateTimeKind.Local);

        schedule.IsDue(lastCheck, now).Should().BeTrue();
    }

    [Fact]
    public void IsDue_ShouldReturnFalse_WhenNowIsBeforeFirstOccurrence()
    {
        // Daily at midnight
        var schedule = new BackupSchedule("0 0 * * *");
        var lastCheck = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local); // just fired midnight
        var now       = new DateTime(2024, 1, 1, 6, 0, 0, DateTimeKind.Local); // 6 hours later

        schedule.IsDue(lastCheck, now).Should().BeFalse();
    }

    [Fact]
    public void IsDue_ShouldReturnTrue_WhenRestartMissedSeveralOccurrences()
    {
        // Every 15 minutes; scheduler was down for 2 hours
        var schedule  = new BackupSchedule("*/15 * * * *");
        var lastCheck = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local);
        var now       = new DateTime(2024, 1, 1, 12, 3, 0, DateTimeKind.Local);

        // Many occurrences were missed — IsDue should be true so the scheduler catches up
        schedule.IsDue(lastCheck, now).Should().BeTrue();
    }

    [Fact]
    public void IsDue_ShouldReturnTrue_WhenLastCheckIsMinValue_FirstBoot()
    {
        // On first boot the tracker returns DateTime.MinValue.
        // The schedule should fire as soon as its next occurrence is <= now.
        var schedule = new BackupSchedule("0 0 * * 0"); // Weekly on Sunday at midnight
        // Pick a Sunday
        var now = new DateTime(2024, 1, 7, 1, 0, 0, DateTimeKind.Local); // Sunday 01:00

        schedule.IsDue(DateTime.MinValue, now).Should().BeTrue();
    }
}
