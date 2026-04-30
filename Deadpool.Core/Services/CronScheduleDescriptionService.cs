using System.Globalization;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

public class CronScheduleDescriptionService : ICronScheduleDescriptionService
{
    public const string FallbackDescription = "on a custom schedule";

    private static readonly Dictionary<string, string> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = "Sunday",
        ["1"] = "Monday",
        ["2"] = "Tuesday",
        ["3"] = "Wednesday",
        ["4"] = "Thursday",
        ["5"] = "Friday",
        ["6"] = "Saturday",
        ["7"] = "Sunday",
        ["SUN"] = "Sunday",
        ["MON"] = "Monday",
        ["TUE"] = "Tuesday",
        ["WED"] = "Wednesday",
        ["THU"] = "Thursday",
        ["FRI"] = "Friday",
        ["SAT"] = "Saturday"
    };

    public string Describe(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return FallbackDescription;

        var parts = cronExpression
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 5)
            return FallbackDescription;

        var minute = parts[0];
        var hour = parts[1];
        var dayOfMonth = parts[2];
        var month = parts[3];
        var dayOfWeek = parts[4];

        if (TryDescribeMinuteInterval(minute, hour, dayOfMonth, month, dayOfWeek, out var intervalDescription))
            return intervalDescription;

        if (TryDescribeFixedTimeSchedule(minute, hour, dayOfMonth, month, dayOfWeek, out var fixedTimeDescription))
            return fixedTimeDescription;

        return FallbackDescription;
    }

    private static bool TryDescribeMinuteInterval(
        string minute,
        string hour,
        string dayOfMonth,
        string month,
        string dayOfWeek,
        out string description)
    {
        description = string.Empty;

        if (hour != "*" || dayOfMonth != "*" || month != "*" || dayOfWeek != "*")
            return false;

        if (!minute.StartsWith("*/", StringComparison.Ordinal))
            return false;

        var rawInterval = minute[2..];
        if (!int.TryParse(rawInterval, NumberStyles.None, CultureInfo.InvariantCulture, out var intervalMinutes))
            return false;

        if (intervalMinutes <= 0)
            return false;

        var unit = intervalMinutes == 1 ? "minute" : "minutes";
        description = $"every {intervalMinutes} {unit}";
        return true;
    }

    private static bool TryDescribeFixedTimeSchedule(
        string minute,
        string hour,
        string dayOfMonth,
        string month,
        string dayOfWeek,
        out string description)
    {
        description = string.Empty;

        if (dayOfMonth != "*" || month != "*")
            return false;

        if (!int.TryParse(minute, NumberStyles.None, CultureInfo.InvariantCulture, out var minuteValue))
            return false;

        if (!int.TryParse(hour, NumberStyles.None, CultureInfo.InvariantCulture, out var hourValue))
            return false;

        if (minuteValue < 0 || minuteValue > 59 || hourValue < 0 || hourValue > 23)
            return false;

        var timePhrase = BuildTimePhrase(hourValue, minuteValue);

        if (dayOfWeek == "*")
        {
            description = $"{timePhrase} every day";
            return true;
        }

        if (TryDescribeDayOfWeek(dayOfWeek, out var dayPhrase))
        {
            description = $"{timePhrase} {dayPhrase}";
            return true;
        }

        return false;
    }

    private static string BuildTimePhrase(int hour, int minute)
    {
        if (hour == 0 && minute == 0)
            return "at midnight";

        return $"at {hour:D2}:{minute:D2}";
    }

    private static bool TryDescribeDayOfWeek(string dayOfWeek, out string dayPhrase)
    {
        dayPhrase = string.Empty;

        if (dayOfWeek.Contains(',', StringComparison.Ordinal))
        {
            var days = dayOfWeek
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(TryResolveDayName)
                .ToList();

            if (days.Any(d => d is null))
                return false;

            dayPhrase = $"every {string.Join(", ", days!)}";
            return true;
        }

        if (dayOfWeek.Contains('-', StringComparison.Ordinal))
        {
            var range = dayOfWeek
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (range.Length != 2)
                return false;

            var start = TryResolveDayName(range[0]);
            var end = TryResolveDayName(range[1]);

            if (start is null || end is null)
                return false;

            dayPhrase = $"{start} through {end}";
            return true;
        }

        var singleDay = TryResolveDayName(dayOfWeek);
        if (singleDay is null)
            return false;

        dayPhrase = $"every {singleDay}";
        return true;
    }

    private static string? TryResolveDayName(string value)
    {
        return DayNames.TryGetValue(value, out var name) ? name : null;
    }
}
