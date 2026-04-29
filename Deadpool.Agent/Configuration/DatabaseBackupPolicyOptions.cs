namespace Deadpool.Agent.Configuration;

// Mirrors one entry under "BackupPolicies" in appsettings.json
public class DatabaseBackupPolicyOptions
{
    public string DatabaseName { get; set; } = string.Empty;
    public string RecoveryModel { get; set; } = "Full";

    // 5-part standard cron expressions (minute-resolution)
    public string FullBackupCron { get; set; } = string.Empty;
    public string DifferentialBackupCron { get; set; } = string.Empty;
    public string TransactionLogBackupCron { get; set; } = string.Empty;

    public int RetentionDays { get; set; } = 14;
}
