using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

public interface IDatabasePulseService
{
    Task<DatabasePulseStatus> CheckAsync(CancellationToken cancellationToken = default);
}
