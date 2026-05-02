using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Executes restore script commands for a restore plan.
/// </summary>
public interface IRestoreExecutionService
{
    /// <summary>
    /// Executes restore commands sequentially and stops on first failure.
    /// </summary>
    Task<RestoreExecutionResult> ExecuteAsync(
        RestorePlan plan,
        bool allowOverwrite,
        CancellationToken cancellationToken = default);
}
