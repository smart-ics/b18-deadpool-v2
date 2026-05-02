namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Result of validating whether a restore plan is physically and logically executable.
/// </summary>
public sealed class RestoreValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public void AddError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Errors.Add(message);
    }

    public void AddWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Warnings.Add(message);
    }
}
