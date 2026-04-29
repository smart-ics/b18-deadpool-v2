using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Estimation;

/// <summary>
/// Conservative backup size estimator using recent backup history.
/// Uses last successful backup size with 20% safety margin.
/// </summary>
public class RecentBackupSizeEstimator : IBackupSizeEstimator
{
    private readonly IBackupJobRepository _backupJobRepository;
    private const decimal SafetyMarginMultiplier = 1.2m; // 20% safety margin

    public RecentBackupSizeEstimator(IBackupJobRepository backupJobRepository)
    {
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
    }

    public async Task<long?> EstimateNextBackupSizeAsync(string databaseName, BackupType backupType)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        var lastSuccessfulBackup = await _backupJobRepository.GetLastSuccessfulBackupAsync(databaseName, backupType);

        if (lastSuccessfulBackup == null || !lastSuccessfulBackup.FileSizeBytes.HasValue || lastSuccessfulBackup.FileSizeBytes.Value == 0)
            return null; // No historical data available

        // Apply conservative 20% safety margin
        var estimatedSize = (long)(lastSuccessfulBackup.FileSizeBytes.Value * SafetyMarginMultiplier);

        return estimatedSize;
    }
}
