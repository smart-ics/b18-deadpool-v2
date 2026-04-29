using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Persistence;

public class InMemoryStorageHealthCheckRepository : IStorageHealthCheckRepository
{
    private readonly List<StorageHealthCheck> _healthChecks = new();
    private readonly object _lock = new();

    public Task CreateAsync(StorageHealthCheck healthCheck)
    {
        if (healthCheck == null)
            throw new ArgumentNullException(nameof(healthCheck));

        lock (_lock)
        {
            _healthChecks.Add(healthCheck);
        }

        return Task.CompletedTask;
    }

    public Task<StorageHealthCheck?> GetLatestHealthCheckAsync(string volumePath)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
            throw new ArgumentException("Volume path cannot be empty.", nameof(volumePath));

        lock (_lock)
        {
            var latest = _healthChecks
                .Where(hc => hc.VolumePath.Equals(volumePath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(hc => hc.CheckTime)
                .FirstOrDefault();

            return Task.FromResult(latest);
        }
    }

    public Task<IEnumerable<StorageHealthCheck>> GetRecentHealthChecksAsync(string volumePath, int count)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
            throw new ArgumentException("Volume path cannot be empty.", nameof(volumePath));

        if (count <= 0)
            throw new ArgumentException("Count must be positive.", nameof(count));

        lock (_lock)
        {
            var recent = _healthChecks
                .Where(hc => hc.VolumePath.Equals(volumePath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(hc => hc.CheckTime)
                .Take(count)
                .ToList();

            return Task.FromResult<IEnumerable<StorageHealthCheck>>(recent);
        }
    }

    public void CleanupOldHealthChecks(TimeSpan retention)
    {
        if (retention <= TimeSpan.Zero)
            throw new ArgumentException("Retention period must be positive.", nameof(retention));

        var cutoffTime = DateTime.UtcNow - retention;

        lock (_lock)
        {
            _healthChecks.RemoveAll(hc => hc.CheckTime < cutoffTime);
        }
    }

    public int GetHealthCheckCount()
    {
        lock (_lock)
        {
            return _healthChecks.Count;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _healthChecks.Clear();
        }
    }
}
