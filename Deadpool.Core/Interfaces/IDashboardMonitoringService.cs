using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Aggregates monitoring data for operational dashboard.
/// </summary>
public interface IDashboardMonitoringService
{
    /// <summary>
    /// Captures current backup health, recent jobs, and storage status.
    /// </summary>
    Task<DashboardSnapshot> GetDashboardSnapshotAsync(string databaseName, string backupVolumePath);
}
