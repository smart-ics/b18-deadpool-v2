namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Ordered SQL restore script commands generated from a restore plan.
/// </summary>
public sealed class RestoreScript
{
    public List<string> Commands { get; }

    public RestoreScript(IEnumerable<string> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var materialized = commands
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        Commands = materialized;
    }

    public string ToSql()
    {
        return string.Join(Environment.NewLine + Environment.NewLine, Commands);
    }
}
