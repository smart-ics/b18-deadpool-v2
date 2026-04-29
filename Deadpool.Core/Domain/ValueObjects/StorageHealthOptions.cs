namespace Deadpool.Core.Domain.ValueObjects;

public record StorageHealthOptions
{
    public decimal WarningThresholdPercentage { get; }
    public decimal CriticalThresholdPercentage { get; }
    public long MinimumWarningFreeSpaceBytes { get; }
    public long MinimumCriticalFreeSpaceBytes { get; }

    public StorageHealthOptions(
        decimal warningThresholdPercentage,
        decimal criticalThresholdPercentage,
        long minimumWarningFreeSpaceBytes,
        long minimumCriticalFreeSpaceBytes)
    {
        if (warningThresholdPercentage <= 0 || warningThresholdPercentage > 100)
            throw new ArgumentException("Warning threshold must be between 0 and 100.", nameof(warningThresholdPercentage));

        if (criticalThresholdPercentage <= 0 || criticalThresholdPercentage > 100)
            throw new ArgumentException("Critical threshold must be between 0 and 100.", nameof(criticalThresholdPercentage));

        if (criticalThresholdPercentage >= warningThresholdPercentage)
            throw new ArgumentException("Critical threshold must be lower than warning threshold.");

        if (minimumWarningFreeSpaceBytes < 0)
            throw new ArgumentException("Minimum warning free space cannot be negative.", nameof(minimumWarningFreeSpaceBytes));

        if (minimumCriticalFreeSpaceBytes < 0)
            throw new ArgumentException("Minimum critical free space cannot be negative.", nameof(minimumCriticalFreeSpaceBytes));

        if (minimumCriticalFreeSpaceBytes >= minimumWarningFreeSpaceBytes)
            throw new ArgumentException("Critical absolute threshold must be lower than warning absolute threshold.");

        WarningThresholdPercentage = warningThresholdPercentage;
        CriticalThresholdPercentage = criticalThresholdPercentage;
        MinimumWarningFreeSpaceBytes = minimumWarningFreeSpaceBytes;
        MinimumCriticalFreeSpaceBytes = minimumCriticalFreeSpaceBytes;
    }

    public static StorageHealthOptions Default => new(
        warningThresholdPercentage: 20,
        criticalThresholdPercentage: 10,
        minimumWarningFreeSpaceBytes: 50L * 1024 * 1024 * 1024,  // 50 GB
        minimumCriticalFreeSpaceBytes: 20L * 1024 * 1024 * 1024); // 20 GB
}
