namespace Deadpool.UI.Configuration;

public class DashboardOptions
{
    public string DatabaseName { get; set; } = "MyHospitalDB";
    public string BackupVolumePath { get; set; } = "C:\\Backups";
    public int AutoRefreshIntervalSeconds { get; set; } = 60;
}

public class DeadpoolOptions
{
    public string SqliteDatabasePath { get; set; } = "deadpool.db";
}
