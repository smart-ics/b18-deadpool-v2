using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Metadata;

public class StubDatabaseConnectivityProbe : IDatabaseConnectivityProbe
{
    public Task ProbeAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Production database not configured — stub connectivity probe.");
}
