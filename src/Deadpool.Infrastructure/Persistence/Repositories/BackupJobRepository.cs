using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence.Repositories;

public class BackupJobRepository : IBackupJobRepository
{
    private readonly string _connectionString;
    private readonly ILogger<BackupJobRepository> _logger;

    public BackupJobRepository(IConfiguration configuration, ILogger<BackupJobRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DeadpoolDb") 
            ?? throw new InvalidOperationException("DeadpoolDb connection string not configured");
        _logger = logger;
    }

    public Task<BackupJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }

    public Task<IEnumerable<BackupJob>> GetByDatabaseAsync(Guid databaseId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupJob>());
    }

    public Task<IEnumerable<BackupJob>> GetByStatusAsync(BackupStatus status, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupJob>());
    }

    public Task<IEnumerable<BackupJob>> GetPendingJobsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupJob>());
    }

    public Task<IEnumerable<BackupJob>> GetJobsInDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupJob>());
    }

    public Task<BackupJob?> GetLatestSuccessfulBackupAsync(Guid databaseId, BackupType backupType, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult<BackupJob?>(null);
    }

    public Task AddAsync(BackupJob job, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BackupJob job, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }
}
