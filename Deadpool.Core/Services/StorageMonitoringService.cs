using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

public class StorageMonitoringService : IStorageMonitoringService
{
    private readonly IStorageInfoProvider _storageInfoProvider;
    private readonly IBackupSizeEstimator? _backupSizeEstimator;
    private readonly StorageHealthOptions _options;
    private const decimal HysteresisRecoveryBufferPercentage = 3m; // Don't recover until 3% above warning threshold

    public StorageMonitoringService(
        IStorageInfoProvider storageInfoProvider,
        StorageHealthOptions options,
        IBackupSizeEstimator? backupSizeEstimator = null)
    {
        _storageInfoProvider = storageInfoProvider ?? throw new ArgumentNullException(nameof(storageInfoProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backupSizeEstimator = backupSizeEstimator;
    }

    public async Task<StorageHealthCheck> CheckStorageHealthAsync(string volumePath)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
            throw new ArgumentException("Volume path cannot be empty.", nameof(volumePath));

        var healthCheck = new StorageHealthCheck(volumePath);

        var isAccessible = await _storageInfoProvider.IsVolumeAccessibleAsync(volumePath);
        if (!isAccessible)
        {
            healthCheck.AddCriticalFinding($"Storage volume unavailable: {volumePath}");
            return healthCheck;
        }

        try
        {
            var (totalBytes, freeBytes) = await _storageInfoProvider.GetStorageInfoAsync(volumePath);
            healthCheck.RecordStorageMetrics(totalBytes, freeBytes);

            EvaluateStorageThresholds(healthCheck, freeBytes);
        }
        catch (Exception ex)
        {
            healthCheck.AddCriticalFinding($"Failed to retrieve storage metrics: {ex.Message}");
        }

        return healthCheck;
    }

    public async Task<StorageHealthCheck> CheckStorageHealthAsync(string volumePath, string databaseName, BackupType nextBackupType)
    {
        var healthCheck = await CheckStorageHealthAsync(volumePath);

        if (!healthCheck.IsHealthy())
            return healthCheck; // Already in warning/critical state

        await EvaluateNextBackupSufficiency(healthCheck, databaseName, nextBackupType);

        return healthCheck;
    }

    private void EvaluateStorageThresholds(StorageHealthCheck healthCheck, long freeBytes)
    {
        var freePercentage = healthCheck.FreePercentage;
        var totalBytes = healthCheck.TotalBytes;

        // Check both percentage AND absolute thresholds - trigger if EITHER violated
        bool isCriticalByPercentage = freePercentage <= _options.CriticalThresholdPercentage;
        bool isCriticalByAbsolute = freeBytes <= _options.MinimumCriticalFreeSpaceBytes;

        bool isWarningByPercentage = freePercentage <= _options.WarningThresholdPercentage;
        bool isWarningByAbsolute = freeBytes <= _options.MinimumWarningFreeSpaceBytes;

        // Hysteresis: only recover to healthy if we exceed the warning threshold + recovery buffer
        // This prevents oscillation around the boundary
        bool hasRecoveryBuffer = freePercentage > _options.WarningThresholdPercentage + HysteresisRecoveryBufferPercentage
                                  && freeBytes > _options.MinimumWarningFreeSpaceBytes;

        if (isCriticalByPercentage || isCriticalByAbsolute)
        {
            var reasons = new List<string>();
            if (isCriticalByPercentage)
                reasons.Add($"percentage {freePercentage:F1}% ≤ {_options.CriticalThresholdPercentage}%");
            if (isCriticalByAbsolute)
                reasons.Add($"absolute {FormatBytes(freeBytes)} ≤ {FormatBytes(_options.MinimumCriticalFreeSpaceBytes)}");

            healthCheck.AddCriticalFinding(
                $"Critically low storage space: {freePercentage:F1}% free ({FormatBytes(freeBytes)} of {FormatBytes(totalBytes)}). " +
                $"Threshold violated: {string.Join(", ", reasons)}.");
        }
        else if (isWarningByPercentage || isWarningByAbsolute)
        {
            var reasons = new List<string>();
            if (isWarningByPercentage)
                reasons.Add($"percentage {freePercentage:F1}% ≤ {_options.WarningThresholdPercentage}%");
            if (isWarningByAbsolute)
                reasons.Add($"absolute {FormatBytes(freeBytes)} ≤ {FormatBytes(_options.MinimumWarningFreeSpaceBytes)}");

            healthCheck.AddWarning(
                $"Low storage space: {freePercentage:F1}% free ({FormatBytes(freeBytes)} of {FormatBytes(totalBytes)}). " +
                $"Threshold violated: {string.Join(", ", reasons)}.");
        }
    }

    private async Task EvaluateNextBackupSufficiency(StorageHealthCheck healthCheck, string databaseName, BackupType nextBackupType)
    {
        if (_backupSizeEstimator == null)
            return; // Predictive check disabled

        try
        {
            var estimatedBackupSize = await _backupSizeEstimator.EstimateNextBackupSizeAsync(databaseName, nextBackupType);

            if (estimatedBackupSize == null)
                return; // No historical data to estimate

            var freeBytes = healthCheck.FreeBytes;
            var remainingAfterBackup = freeBytes - estimatedBackupSize.Value;

            // Check if backup will leave us below critical threshold
            if (remainingAfterBackup <= _options.MinimumCriticalFreeSpaceBytes)
            {
                healthCheck.AddCriticalFinding(
                    $"Next {nextBackupType} backup for {databaseName} (est. {FormatBytes(estimatedBackupSize.Value)}) " +
                    $"will leave {FormatBytes(remainingAfterBackup)} free, below critical threshold {FormatBytes(_options.MinimumCriticalFreeSpaceBytes)}.");
            }
            // Check if backup will leave us below warning threshold
            else if (remainingAfterBackup <= _options.MinimumWarningFreeSpaceBytes)
            {
                healthCheck.AddWarning(
                    $"Next {nextBackupType} backup for {databaseName} (est. {FormatBytes(estimatedBackupSize.Value)}) " +
                    $"will leave {FormatBytes(remainingAfterBackup)} free, below warning threshold {FormatBytes(_options.MinimumWarningFreeSpaceBytes)}.");
            }
        }
        catch
        {
            // Swallow estimation errors - predictive check is best-effort
            // Don't let estimation failures block health reporting
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
