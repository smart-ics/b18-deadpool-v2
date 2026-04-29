using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Interfaces;

public interface IBackupHealthCheckRepository
{
    Task CreateAsync(BackupHealthCheck healthCheck);
    Task<BackupHealthCheck?> GetLatestHealthCheckAsync(string databaseName);
    Task<IEnumerable<BackupHealthCheck>> GetRecentHealthChecksAsync(string databaseName, int count);
    void CleanupOldHealthChecks(TimeSpan retention);
}
