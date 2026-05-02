using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Executes restore operations and enforces mandatory restore plan validation beforehand.
/// </summary>
public interface IRestoreExecutionService
{
    /// <summary>
    /// Executes a restore pipeline delegate only after validation succeeds.
    /// </summary>
    Task ExecuteAsync(
        RestorePlan plan,
        Func<RestorePlan, CancellationToken, Task> executeRestoreAsync,
        CancellationToken cancellationToken = default);
}
