using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Domain.Repositories;

/// <summary>
/// Repository interface for SqlServerInstance aggregate
/// </summary>
public interface ISqlServerInstanceRepository
{
    Task<SqlServerInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SqlServerInstance?> GetByServerNameAsync(string serverName, string? instanceName = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<SqlServerInstance>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SqlServerInstance>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SqlServerInstance instance, CancellationToken cancellationToken = default);
    Task UpdateAsync(SqlServerInstance instance, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
