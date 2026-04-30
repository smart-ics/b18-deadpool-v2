using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Dashboard view model for monitoring UI.
/// Aggregates backup health, recent jobs, and storage status.
/// </summary>
public record DashboardSnapshot
{
    public DateTime SnapshotTime { get; init; }
    public string DatabaseName { get; init; }
    public LastBackupStatus LastBackupStatus { get; init; }
    public ChainInitializationStatusSummary ChainInitializationStatus { get; init; }
    public List<RecentJobSummary> RecentJobs { get; init; }
    public StorageStatusSummary StorageStatus { get; init; }

    public DashboardSnapshot(
        string databaseName,
        LastBackupStatus lastBackupStatus,
        ChainInitializationStatusSummary chainInitializationStatus,
        List<RecentJobSummary> recentJobs,
        StorageStatusSummary storageStatus)
    {
        SnapshotTime = DateTime.UtcNow;
        DatabaseName = databaseName;
        LastBackupStatus = lastBackupStatus;
        ChainInitializationStatus = chainInitializationStatus;
        RecentJobs = recentJobs;
        StorageStatus = storageStatus;
    }
}

public record ChainInitializationStatusSummary
{
    public bool? IsInitialized { get; init; }
    public DateTime? LastValidFullBackupTime { get; init; }
    public string? LastValidFullBackupPath { get; init; }
    public string RestoreChainHealth { get; init; } = "Unknown";
    public string WarningMessage { get; init; } = string.Empty;
}

public record LastBackupStatus
{
    public DateTime? LastFullBackup { get; init; }
    public DateTime? LastDifferentialBackup { get; init; }
    public DateTime? LastLogBackup { get; init; }
    public HealthStatus OverallHealth { get; init; }
    public List<string> Warnings { get; init; }
    public List<string> CriticalIssues { get; init; }
    public string ChainHealthSummary { get; init; }

    public LastBackupStatus()
    {
        Warnings = new List<string>();
        CriticalIssues = new List<string>();
        ChainHealthSummary = "Unknown";
    }
}

public record RecentJobSummary
{
    public BackupType BackupType { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public BackupStatus Status { get; init; }
    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue 
        ? EndTime.Value - StartTime.Value 
        : null;
    public string FilePath { get; init; }
    public string? ErrorMessage { get; init; }

    public RecentJobSummary(
        BackupType backupType,
        DateTime? startTime,
        DateTime? endTime,
        BackupStatus status,
        string filePath,
        string? errorMessage = null)
    {
        BackupType = backupType;
        StartTime = startTime;
        EndTime = endTime;
        Status = status;
        FilePath = filePath;
        ErrorMessage = errorMessage;
    }
}

public record StorageStatusSummary
{
    public string VolumePath { get; init; }
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public decimal FreePercentage { get; init; }
    public HealthStatus OverallHealth { get; init; }
    public List<string> Warnings { get; init; }
    public List<string> CriticalIssues { get; init; }

    public StorageStatusSummary(
        string volumePath,
        long totalBytes,
        long freeBytes,
        decimal freePercentage,
        HealthStatus overallHealth,
        List<string> warnings,
        List<string> criticalIssues)
    {
        VolumePath = volumePath;
        TotalBytes = totalBytes;
        FreeBytes = freeBytes;
        FreePercentage = freePercentage;
        OverallHealth = overallHealth;
        Warnings = warnings;
        CriticalIssues = criticalIssues;
    }
}
