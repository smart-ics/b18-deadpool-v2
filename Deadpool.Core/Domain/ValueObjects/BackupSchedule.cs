namespace Deadpool.Core.Domain.ValueObjects;

public record BackupSchedule
{
    public string CronExpression { get; init; }

    public BackupSchedule(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be empty.", nameof(cronExpression));

        CronExpression = cronExpression;
    }
}
