using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Core.Services;

public class DatabasePulseService : IDatabasePulseService
{
    private readonly IDatabaseConnectivityProbe _probe;
    private readonly ILogger<DatabasePulseService> _logger;

    public DatabasePulseService(
        IDatabaseConnectivityProbe probe,
        ILogger<DatabasePulseService> logger)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DatabasePulseStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        try
        {
            await _probe.ProbeAsync(cancellationToken);
            return new DatabasePulseStatus(HealthStatus.Healthy, now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database pulse check failed.");
            return new DatabasePulseStatus(HealthStatus.Critical, now, ex.Message);
        }
    }
}
