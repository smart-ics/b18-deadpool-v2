using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

public class BackupHealthCheck
{
    public string DatabaseName { get; }
    public DateTime CheckTime { get; }
    public HealthStatus OverallHealth { get; private set; }
    public DateTime? LastSuccessfulFullBackup { get; private set; }
    public DateTime? LastSuccessfulDifferentialBackup { get; private set; }
    public DateTime? LastSuccessfulLogBackup { get; private set; }
    public DateTime? LastFailedBackup { get; private set; }
    public List<string> Warnings { get; }
    public List<string> CriticalFindings { get; }
    public List<string> Limitations { get; }

    public BackupHealthCheck(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        DatabaseName = databaseName;
        CheckTime = DateTime.Now;
        OverallHealth = HealthStatus.Healthy;
        Warnings = new List<string>();
        CriticalFindings = new List<string>();
        Limitations = new List<string>();
    }

    public void RecordLastSuccessfulFullBackup(DateTime backupTime)
    {
        LastSuccessfulFullBackup = backupTime;
    }

    public void RecordLastSuccessfulDifferentialBackup(DateTime backupTime)
    {
        LastSuccessfulDifferentialBackup = backupTime;
    }

    public void RecordLastSuccessfulLogBackup(DateTime backupTime)
    {
        LastSuccessfulLogBackup = backupTime;
    }

    public void RecordLastFailedBackup(DateTime backupTime)
    {
        LastFailedBackup = backupTime;
    }

    public void AddWarning(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            throw new ArgumentException("Warning message cannot be empty.", nameof(warning));

        Warnings.Add(warning);

        if (OverallHealth == HealthStatus.Healthy)
            OverallHealth = HealthStatus.Warning;
    }

    public void AddCriticalFinding(string finding)
    {
        if (string.IsNullOrWhiteSpace(finding))
            throw new ArgumentException("Critical finding message cannot be empty.", nameof(finding));

        CriticalFindings.Add(finding);
        OverallHealth = HealthStatus.Critical;
    }

    public void AddLimitation(string limitation)
    {
        if (string.IsNullOrWhiteSpace(limitation))
            throw new ArgumentException("Limitation message cannot be empty.", nameof(limitation));

        Limitations.Add(limitation);
    }

    public bool IsHealthy() => OverallHealth == HealthStatus.Healthy;
    public bool HasWarnings() => OverallHealth == HealthStatus.Warning;
    public bool IsCritical() => OverallHealth == HealthStatus.Critical;

    private BackupHealthCheck(
        string databaseName,
        DateTime checkTime,
        HealthStatus overallHealth,
        DateTime? lastSuccessfulFullBackup,
        DateTime? lastSuccessfulDifferentialBackup,
        DateTime? lastSuccessfulLogBackup,
        DateTime? lastFailedBackup,
        List<string> warnings,
        List<string> criticalFindings,
        List<string> limitations)
    {
        DatabaseName = databaseName;
        CheckTime = checkTime;
        OverallHealth = overallHealth;
        LastSuccessfulFullBackup = lastSuccessfulFullBackup;
        LastSuccessfulDifferentialBackup = lastSuccessfulDifferentialBackup;
        LastSuccessfulLogBackup = lastSuccessfulLogBackup;
        LastFailedBackup = lastFailedBackup;
        Warnings = warnings;
        CriticalFindings = criticalFindings;
        Limitations = limitations;
    }

    public static BackupHealthCheck Restore(
        string databaseName,
        DateTime checkTime,
        HealthStatus overallHealth,
        DateTime? lastSuccessfulFullBackup,
        DateTime? lastSuccessfulDifferentialBackup,
        DateTime? lastSuccessfulLogBackup,
        DateTime? lastFailedBackup,
        List<string> warnings,
        List<string> criticalFindings,
        List<string> limitations)
        => new BackupHealthCheck(
            databaseName, checkTime, overallHealth,
            lastSuccessfulFullBackup, lastSuccessfulDifferentialBackup,
            lastSuccessfulLogBackup, lastFailedBackup,
            warnings, criticalFindings, limitations);
}
