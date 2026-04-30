using Deadpool.Core.Services;
using FluentAssertions;

namespace Deadpool.Tests.Services;

public class CronScheduleDescriptionServiceTests
{
    private readonly CronScheduleDescriptionService _service = new();

    [Fact]
    public void Describe_ShouldFormatWeeklyMidnightSchedule_WhenSundayCron()
    {
        // Arrange
        const string cron = "0 0 * * 0";

        // Act
        var description = _service.Describe(cron);

        // Assert
        description.Should().Be("at midnight every Sunday");
    }

    [Fact]
    public void Describe_ShouldFormatWeekdayRangeSchedule_WhenMonThroughSatCron()
    {
        // Arrange
        const string cron = "0 0 * * 1-6";

        // Act
        var description = _service.Describe(cron);

        // Assert
        description.Should().Be("at midnight Monday through Saturday");
    }

    [Fact]
    public void Describe_ShouldFormatIntervalSchedule_WhenEvery15MinutesCron()
    {
        // Arrange
        const string cron = "*/15 * * * *";

        // Act
        var description = _service.Describe(cron);

        // Assert
        description.Should().Be("every 15 minutes");
    }

    [Fact]
    public void Describe_ShouldReturnFallback_WhenUnsupportedCronPattern()
    {
        // Arrange
        const string unsupportedCron = "0 0 1 * *";

        // Act
        var description = _service.Describe(unsupportedCron);

        // Assert
        description.Should().Be("on a custom schedule");
    }

    [Fact]
    public void Describe_ShouldReturnFallback_WhenCronIsInvalid()
    {
        // Arrange
        const string invalidCron = "not-a-cron";

        // Act
        var description = _service.Describe(invalidCron);

        // Assert
        description.Should().Be("on a custom schedule");
    }

    [Fact]
    public void Describe_ShouldProduceReadableSentence_WhenFallbackUsed()
    {
        // Arrange — unsupported pattern triggers fallback
        const string unsupportedCron = "0 0 1 * *";
        const string prefix = "Full Backup runs ";

        // Act
        var description = _service.Describe(unsupportedCron);

        // Assert
        var sentence = $"{prefix}{description}";
        sentence.Should().Be("Full Backup runs on a custom schedule");
    }
}
