using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Provides backup size estimation for predictive storage monitoring.
/// </summary>
public interface IBackupSizeEstimator
{
    /// <summary>
    /// Estimates the size of the next backup for a database.
    /// Returns null if no estimation data is available.
    /// </summary>
    Task<long?> EstimateNextBackupSizeAsync(string databaseName, BackupType backupType);
}
