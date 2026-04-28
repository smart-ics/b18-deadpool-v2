using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Domain.Repositories;

/// <summary>
/// Repository interface for Database aggregate
/// </summary>
public interface IDatabaseRepository
{
    Task<Database?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Database?> GetByNameAsync(Guid sqlServerInstanceId, string databaseName, CancellationToken cancellationToken = default);
    Task<IEnumerable<Database>> GetByServerInstanceAsync(Guid sqlServerInstanceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Database>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Database database, CancellationToken cancellationToken = default);
    Task UpdateAsync(Database database, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
