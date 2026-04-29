using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Tracks per-database backup chain initialization state across restarts.
/// Implementations must be safe to call from multiple threads.
/// </summary>
public interface IBootstrapStateTracker
{
    BackupChainInitializationStatus GetStatus(string databaseName);
    void SetStatus(string databaseName, BackupChainInitializationStatus status);
}
