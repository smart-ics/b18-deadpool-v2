using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence.Repositories;

public class DatabaseRepository : IDatabaseRepository
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseRepository> _logger;

    public DatabaseRepository(IConfiguration configuration, ILogger<DatabaseRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DeadpoolDb") 
            ?? throw new InvalidOperationException("DeadpoolDb connection string not configured");
        _logger = logger;
    }

    public Task<Database?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }

    public Task<Database?> GetByNameAsync(Guid sqlServerInstanceId, string databaseName, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Database>> GetByServerInstanceAsync(Guid sqlServerInstanceId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<Database>());
    }

    public Task<IEnumerable<Database>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.FromResult(Enumerable.Empty<Database>());
    }

    public Task AddAsync(Database database, CancellationToken cancellationToken = default)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Database database, CancellationToken cancellationToken = default)
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
