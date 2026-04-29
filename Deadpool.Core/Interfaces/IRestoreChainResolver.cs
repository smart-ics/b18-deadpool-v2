using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Service for resolving backup restore chains.
/// Planning only - does not execute restore operations.
/// </summary>
public interface IRestoreChainResolver
{
    /// <summary>
    /// Resolves the backup chain required to restore to a specific point in time.
    /// Uses LSN-aware logic to determine the minimal valid restore sequence.
    /// </summary>
    /// <param name="databaseName">Target database name</param>
    /// <param name="restorePoint">Target point-in-time for restore</param>
    /// <returns>
    /// RestorePlan with selected backups and sequence, or invalid plan with failure reason.
    /// </returns>
    Task<RestorePlan> ResolveRestoreChainAsync(string databaseName, DateTime restorePoint);
}
