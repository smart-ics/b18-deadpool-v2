using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Repositories;

/// <summary>
/// Repository interface for BackupSchedule aggregate
/// </summary>
public interface IBackupScheduleRepository
{
    Task<BackupSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupSchedule>> GetByDatabaseAsync(Guid databaseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupSchedule>> GetEnabledSchedulesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupSchedule>> GetSchedulesDueForExecutionAsync(DateTime beforeTime, CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupSchedule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(BackupSchedule schedule, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackupSchedule schedule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
