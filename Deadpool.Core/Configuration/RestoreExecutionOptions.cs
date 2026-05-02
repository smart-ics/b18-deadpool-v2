namespace Deadpool.Core.Configuration;

/// <summary>
/// Runtime options for SQL restore execution.
/// </summary>
public sealed class RestoreExecutionOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 300;
}
