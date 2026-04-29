using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Service for safely cleaning up expired backup files.
/// Conservative approach: Prioritizes chain safety over aggressive deletion.
/// </summary>
public interface IRetentionCleanupService
{
    /// <summary>
    /// Evaluates and deletes expired backup files for a database.
    /// Preserves restore chain safety - never deletes backups needed for recovery.
    /// </summary>
    /// <param name="databaseName">Database to clean up backups for</param>
    /// <param name="retentionPolicy">Retention policy defining retention periods</param>
    /// <param name="isDryRun">If true, evaluates what would be deleted without actually deleting</param>
    /// <returns>Result containing what was evaluated, deleted, and retained</returns>
    Task<RetentionCleanupResult> CleanupExpiredBackupsAsync(
        string databaseName,
        RetentionPolicy retentionPolicy,
        bool isDryRun = false);
}
