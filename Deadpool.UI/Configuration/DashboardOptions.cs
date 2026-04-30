namespace Deadpool.UI.Configuration;

public class DashboardOptions
{
    public string DatabaseName { get; set; } = "MyHospitalDB";
    public string DatabaseConnectionString { get; set; } = string.Empty;
    public string BackupVolumePath { get; set; } = "C:\\Backups";
    public int AutoRefreshIntervalSeconds { get; set; } = 60;
}

public class DeadpoolDbOptions
{
    public string Path { get; set; } = string.Empty;
}

public class DatabaseBackupPolicyOptions
{
    public string DatabaseName { get; set; } = string.Empty;
    public string RecoveryModel { get; set; } = "Full";
    public string FullBackupCron { get; set; } = string.Empty;
    public string DifferentialBackupCron { get; set; } = string.Empty;
    public string TransactionLogBackupCron { get; set; } = string.Empty;
    public int RetentionDays { get; set; } = 14;
    public bool? BootstrapFullBackupEnabled { get; set; }
}
