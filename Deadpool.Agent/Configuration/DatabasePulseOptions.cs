namespace Deadpool.Agent.Configuration;

public class DatabasePulseOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int RetentionDays { get; set; } = 7;
}
