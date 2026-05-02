namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Per-command execution log for restore script execution.
/// </summary>
public sealed class RestoreStepLog
{
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
