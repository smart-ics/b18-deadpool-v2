namespace Deadpool.Core.Interfaces;

public interface IDatabaseConnectivityProbe
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
