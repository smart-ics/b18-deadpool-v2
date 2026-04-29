namespace Deadpool.Agent.Configuration;

public class HealthMonitoringOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan FullBackupOverdueThreshold { get; set; } = TimeSpan.FromHours(26);
    public TimeSpan DifferentialBackupOverdueThreshold { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan LogBackupOverdueThreshold { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan ChainLookbackPeriod { get; set; } = TimeSpan.FromDays(7);
    public int HealthCheckRetentionDays { get; set; } = 7;
}
