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

    // Returns the next UTC occurrence strictly after fromUtc, or null if none.
    public DateTime? GetNextOccurrence(DateTime fromUtc)
    {
        // DateTime.MinValue has Kind=Unspecified; Cronos requires Kind=Utc.
        // Treat Unspecified as Utc — callers should pass Utc, but defend here.
        if (fromUtc.Kind != DateTimeKind.Utc)
            fromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);

        return _parsed.GetNextOccurrence(fromUtc, TimeZoneInfo.Utc);
    }

    // A schedule is due when at least one occurrence falls in (lastCheckUtc, nowUtc].
    public bool IsDue(DateTime lastCheckUtc, DateTime nowUtc)
    {
        var next = GetNextOccurrence(lastCheckUtc);
        return next.HasValue && next.Value <= nowUtc;
    }

    // Returns the latest occurrence that falls in (lastCheckUtc, nowUtc], i.e. the
    // canonical "what just fired" time.  This is used to advance the tracker so that
    // multiple ticks within the same inter-occurrence window all converge on the same
    // anchor, preventing duplicate job creation.
    public DateTime? GetMostRecentOccurrence(DateTime lastCheckUtc, DateTime nowUtc)
    {
        DateTime? candidate = null;
        var cursor = lastCheckUtc;

        while (true)
        {
            var next = GetNextOccurrence(cursor);
            if (!next.HasValue || next.Value > nowUtc)
                break;

            candidate = next.Value;
            cursor = next.Value;
        }

        return candidate;
    }
}
