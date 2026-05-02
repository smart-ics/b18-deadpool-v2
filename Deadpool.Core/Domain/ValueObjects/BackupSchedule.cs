using Cronos;

namespace Deadpool.Core.Domain.ValueObjects;

public record BackupSchedule
{
    public string CronExpression { get; init; }

    // Parsed once at construction; CronFormat.Standard = 5-part (no seconds)
    private readonly global::Cronos.CronExpression _parsed;

    public BackupSchedule(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be empty.", nameof(cronExpression));

        try
        {
            _parsed = global::Cronos.CronExpression.Parse(cronExpression, CronFormat.Standard);
        }
        catch (CronFormatException ex)
        {
            throw new ArgumentException(
                $"Invalid cron expression '{cronExpression}': {ex.Message}",
                nameof(cronExpression), ex);
        }

        CronExpression = cronExpression;
    }

    // Returns the next local-time occurrence strictly after 'from', or null if none.
    public DateTime? GetNextOccurrence(DateTime from)
    {
        // Treat Unspecified as Local. Convert Utc to Local for callers that still pass Utc.
        if (from.Kind == DateTimeKind.Utc)
            from = from.ToLocalTime();
        else if (from.Kind == DateTimeKind.Unspecified)
            from = DateTime.SpecifyKind(from, DateTimeKind.Local);

        return _parsed.GetNextOccurrence(from, TimeZoneInfo.Local);
    }

    // A schedule is due when at least one occurrence falls in (lastCheck, now].
    public bool IsDue(DateTime lastCheck, DateTime now)
    {
        var next = GetNextOccurrence(lastCheck);
        return next.HasValue && next.Value <= now;
    }

    // Returns the latest occurrence that falls in (lastCheck, now], i.e. the
    // canonical "what just fired" time.  This is used to advance the tracker so that
    // multiple ticks within the same inter-occurrence window all converge on the same
    // anchor, preventing duplicate job creation.
    //
    // Implementation note: For first boot or long downtime (lastCheck far in the past),
    // we limit the backward scan to a reasonable window (30 days) to avoid excessive
    // iteration. This is safe because we only need the most recent occurrence, not all
    // historical occurrences.
    public DateTime? GetMostRecentOccurrence(DateTime lastCheck, DateTime now)
    {
        // If lastCheck is more than 30 days before now, start from 30 days ago instead.
        // This prevents walking through years of history on first boot.
        var searchStart = lastCheck;
        var maxLookback = TimeSpan.FromDays(30);

        if (now - lastCheck > maxLookback)
        {
            searchStart = now - maxLookback;
        }

        DateTime? candidate = null;
        var cursor = searchStart;

        while (true)
        {
            var next = GetNextOccurrence(cursor);
            if (!next.HasValue || next.Value > now)
                break;

            candidate = next.Value;
            cursor = next.Value;
        }

        return candidate;
    }
}
