using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Determines backup chain initialization state and performs one-time bootstrap.
/// </summary>
public interface IBackupChainInitializationService
{
    /// <summary>
    /// Checks whether a valid Full backup already exists for the given database.
    /// Does not change any state.
    /// </summary>
    Task<bool> IsChainInitializedAsync(string databaseName);

    /// <summary>
    /// Executes a bootstrap Full backup and marks the chain initialized on success.
    /// Returns true when bootstrap succeeds, false when it fails.
    /// </summary>
    Task<bool> BootstrapAsync(string databaseName, CancellationToken cancellationToken);
}
