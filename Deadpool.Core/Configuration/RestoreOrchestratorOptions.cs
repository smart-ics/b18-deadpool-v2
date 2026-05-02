namespace Deadpool.Core.Configuration;

/// <summary>
/// Runtime settings used by restore orchestration entry point.
/// </summary>
public sealed class RestoreOrchestratorOptions
{
    public string DatabaseName { get; set; } = string.Empty;
}
