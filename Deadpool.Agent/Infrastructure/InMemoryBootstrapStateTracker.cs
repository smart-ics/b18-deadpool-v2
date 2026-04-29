using System.Collections.Concurrent;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Agent.Infrastructure;

/// <summary>
/// Thread-safe in-memory tracker for per-database bootstrap state.
/// State resets to unknown on every service restart; the <see cref="BootstrapWorker"/>
/// re-checks the repository on startup to restore actual state.
/// </summary>
public sealed class InMemoryBootstrapStateTracker : IBootstrapStateTracker
{
    private readonly ConcurrentDictionary<string, BackupChainInitializationStatus> _state
        = new(StringComparer.OrdinalIgnoreCase);

    public BackupChainInitializationStatus GetStatus(string databaseName)
        => _state.GetValueOrDefault(databaseName, BackupChainInitializationStatus.BootstrapPending);

    public void SetStatus(string databaseName, BackupChainInitializationStatus status)
        => _state[databaseName] = status;
}
