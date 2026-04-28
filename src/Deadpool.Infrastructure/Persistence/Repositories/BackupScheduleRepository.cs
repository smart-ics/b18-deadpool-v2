using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence.Repositories;

public class BackupScheduleRepository : IBackupScheduleRepository
{
    private readonly string _connectionString;
    private readonly ILogger<BackupScheduleRepository> _logger;

    public BackupScheduleRepository(IConfiguration configuration, ILogger<BackupScheduleRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DeadpoolDb") 
            ?? throw new InvalidOperationException("DeadpoolDb connection string not configured");
        _logger = logger;
    }

    public Task<BackupSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }

    public Task<IEnumerable<BackupSchedule>> GetByDatabaseAsync(Guid databaseId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupSchedule>());
    }

    public Task<IEnumerable<BackupSchedule>> GetEnabledSchedulesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupSchedule>());
    }

    public Task<IEnumerable<BackupSchedule>> GetSchedulesDueForExecutionAsync(DateTime beforeTime, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupSchedule>());
    }

    public Task<IEnumerable<BackupSchedule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<BackupSchedule>());
    }

    public Task AddAsync(BackupSchedule schedule, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BackupSchedule schedule, CancellationToken cancellationToken = default)
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
