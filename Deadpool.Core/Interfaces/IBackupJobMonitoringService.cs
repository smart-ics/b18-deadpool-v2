using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Service for backup job monitoring and history queries.
/// </summary>
public interface IBackupJobMonitoringService
{
    /// <summary>
    /// Gets backup job history based on filter criteria.
    /// </summary>
    Task<List<BackupJobDisplayModel>> GetBackupJobHistoryAsync(BackupJobFilter filter);

    /// <summary>
    /// Gets count of jobs by status for summary display.
    /// </summary>
    Task<Dictionary<string, int>> GetJobStatusSummaryAsync(string databaseName);
}
