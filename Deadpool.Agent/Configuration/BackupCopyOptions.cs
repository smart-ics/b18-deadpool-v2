namespace Deadpool.Agent.Configuration;

public class BackupCopyOptions
{
    public bool Enabled { get; set; } = false;
    public string RemoteStoragePath { get; set; } = string.Empty;
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(10);
}
