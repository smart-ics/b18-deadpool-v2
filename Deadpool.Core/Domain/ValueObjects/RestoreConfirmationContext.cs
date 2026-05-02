namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Operator confirmation input required before executing a destructive restore.
/// </summary>
public sealed class RestoreConfirmationContext
{
    public string DatabaseName { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public string? ConfirmationText { get; set; }
    public bool RequireTextMatch { get; set; }
}
