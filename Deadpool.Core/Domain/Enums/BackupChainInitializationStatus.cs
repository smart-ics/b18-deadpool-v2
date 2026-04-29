namespace Deadpool.Core.Domain.Enums;

public enum BackupChainInitializationStatus
{
    /// <summary>A valid Full backup exists; Differential and Log backups may proceed.</summary>
    Initialized,

    /// <summary>No valid Full backup found; bootstrap Full backup is pending.</summary>
    BootstrapPending,

    /// <summary>Bootstrap Full backup attempt failed; chain progression is blocked.</summary>
    BootstrapFailed
}
