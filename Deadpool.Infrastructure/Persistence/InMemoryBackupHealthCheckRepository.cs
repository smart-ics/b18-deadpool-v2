using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Persistence;

public sealed class InMemoryBackupHealthCheckRepository : IBackupHealthCheckRepository
{
    private readonly List<BackupHealthCheck> _healthChecks = new();
    private readonly object _lock = new();

    public Task CreateAsync(BackupHealthCheck healthCheck)
    {
        if (healthCheck == null)
            throw new ArgumentNullException(nameof(healthCheck));

        lock (_lock)
        {
            _healthChecks.Add(healthCheck);
        }

        return Task.CompletedTask;
    }

    public Task<BackupHealthCheck?> GetLatestHealthCheckAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var latest = _healthChecks
                .Where(h => h.DatabaseName == databaseName)
                .OrderByDescending(h => h.CheckTime)
                .FirstOrDefault();

            return Task.FromResult(latest);
        }
    }

    public Task<IEnumerable<BackupHealthCheck>> GetRecentHealthChecksAsync(string databaseName, int count)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var recent = _healthChecks
                .Where(h => h.DatabaseName == databaseName)
                .OrderByDescending(h => h.CheckTime)
                .Take(count)
                .ToList();

            return Task.FromResult<IEnumerable<BackupHealthCheck>>(recent);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _healthChecks.Clear();
        }
    }

    public void CleanupOldHealthChecks(TimeSpan retention)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - retention;
            _healthChecks.RemoveAll(h => h.CheckTime < cutoff);
        }
    }

    public int GetHealthCheckCount()
    {
        lock (_lock)
        {
            return _healthChecks.Count;
        }
    }
}
