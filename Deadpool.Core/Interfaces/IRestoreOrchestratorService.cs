using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Orchestrates restore plan build, validation, and guarded execution.
/// </summary>
public interface IRestoreOrchestratorService
{
    /// <summary>
    /// Executes restore orchestration for the configured target database.
    /// </summary>
    Task ExecuteRestore(DateTime targetTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes restore orchestration using explicit runtime confirmation context.
    /// </summary>
    Task ExecuteRestore(
        DateTime targetTime,
        RestoreConfirmationContext confirmationContext,
        CancellationToken cancellationToken = default);
}
