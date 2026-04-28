using Deadpool.Core.Domain.ValueObjects;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class BackupScheduleTests
{
    [Fact]
    public void Constructor_ShouldCreateInstance_WhenValidCronExpression()
    {
        var cronExpression = "0 2 * * 0";
        var schedule = new BackupSchedule(cronExpression);

        schedule.CronExpression.Should().Be(cronExpression);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenCronExpressionEmpty(string cronExpression)
    {
        var act = () => new BackupSchedule(cronExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Cron expression cannot be empty.*");
    }

    [Fact]
    public void BackupSchedule_ShouldSupportRecordEquality()
    {
        var schedule1 = new BackupSchedule("0 2 * * 0");
        var schedule2 = new BackupSchedule("0 2 * * 0");
        var schedule3 = new BackupSchedule("0 3 * * *");

        schedule1.Should().Be(schedule2);
        schedule1.Should().NotBe(schedule3);
    }
}
