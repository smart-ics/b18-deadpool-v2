using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Interfaces;

public interface IStorageHealthCheckRepository
{
    Task CreateAsync(StorageHealthCheck healthCheck);
    Task<StorageHealthCheck?> GetLatestHealthCheckAsync(string volumePath);
    Task<IEnumerable<StorageHealthCheck>> GetRecentHealthChecksAsync(string volumePath, int count);
    void CleanupOldHealthChecks(TimeSpan retention);
}
