namespace Deadpool.Agent.Configuration;

public class BackupCopyOptions
{
    /// <summary>
    /// Network share path where backup files will be copied.
    /// Example: \\\\BackupServer\\Backups or Z:\\Backups
    /// Leave empty to disable backup copying.
    /// </summary>
    public string RemoteStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of retry attempts for transient copy failures.
    /// Default: 3
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether to enable backup file copying.
    /// If false, files remain only in local backup directory.
    /// </summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(RemoteStoragePath);
}
