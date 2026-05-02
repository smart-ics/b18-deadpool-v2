namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Result of restore script execution.
/// </summary>
public sealed class RestoreExecutionResult
{
    public bool Success { get; set; }
    public List<RestoreStepLog> Steps { get; } = new();
    public string? ErrorMessage { get; set; }
}
