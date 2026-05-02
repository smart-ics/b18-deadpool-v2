using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Builds a restore plan (planning only, no restore execution).
/// </summary>
public interface IRestorePlannerService
{
    /// <summary>
    /// Builds the restore plan for a target point in time.
    /// </summary>
    Task<RestorePlan> BuildRestorePlanAsync(string databaseName, DateTime targetTime);
}
