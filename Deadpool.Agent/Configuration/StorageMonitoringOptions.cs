namespace Deadpool.Agent.Configuration;

public class StorageMonitoringOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(10);
    public decimal WarningThresholdPercentage { get; set; } = 20m;
    public decimal CriticalThresholdPercentage { get; set; } = 10m;
    public long MinimumWarningFreeSpaceGB { get; set; } = 50; // GB
    public long MinimumCriticalFreeSpaceGB { get; set; } = 20; // GB
    public int HealthCheckRetentionDays { get; set; } = 7;
    public List<string> MonitoredVolumes { get; set; } = new();
}
