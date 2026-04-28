namespace Deadpool.Core.Domain.Enums;

/// <summary>
/// Status of a backup job execution
/// </summary>
public enum BackupStatus
{
    /// <summary>
    /// Job is queued and waiting to execute
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Job is currently executing
    /// </summary>
    Running = 1,

    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Job failed with errors
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Job was cancelled by user
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Job completed with warnings (non-critical issues)
    /// </summary>
    CompletedWithWarnings = 5
}
