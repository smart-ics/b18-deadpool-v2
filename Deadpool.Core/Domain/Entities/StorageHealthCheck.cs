using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

public class StorageHealthCheck
{
    public DateTime CheckTime { get; }
    public string VolumePath { get; }
    public long TotalBytes { get; private set; }
    public long FreeBytes { get; private set; }
    public decimal FreePercentage => TotalBytes > 0 ? (decimal)FreeBytes / TotalBytes * 100 : 0;
    public HealthStatus OverallHealth { get; private set; }
    public List<string> Warnings { get; }
    public List<string> CriticalFindings { get; }

    public StorageHealthCheck(string volumePath)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
            throw new ArgumentException("Volume path cannot be empty.", nameof(volumePath));

        CheckTime = DateTime.UtcNow;
        VolumePath = volumePath;
        OverallHealth = HealthStatus.Healthy;
        Warnings = new List<string>();
        CriticalFindings = new List<string>();
    }

    public void RecordStorageMetrics(long totalBytes, long freeBytes)
    {
        if (totalBytes < 0)
            throw new ArgumentException("Total bytes cannot be negative.", nameof(totalBytes));
        if (freeBytes < 0)
            throw new ArgumentException("Free bytes cannot be negative.", nameof(freeBytes));
        if (freeBytes > totalBytes)
            throw new ArgumentException("Free bytes cannot exceed total bytes.");

        TotalBytes = totalBytes;
        FreeBytes = freeBytes;
    }

    public void AddWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Warning message cannot be empty.", nameof(message));

        if (OverallHealth == HealthStatus.Healthy)
            OverallHealth = HealthStatus.Warning;

        Warnings.Add(message);
    }

    public void AddCriticalFinding(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Critical finding message cannot be empty.", nameof(message));

        OverallHealth = HealthStatus.Critical;
        CriticalFindings.Add(message);
    }

    public bool IsHealthy() => OverallHealth == HealthStatus.Healthy;
}
