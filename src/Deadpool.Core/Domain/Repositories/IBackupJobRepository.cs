using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Repositories;

/// <summary>
/// Repository interface for BackupJob aggregate
/// </summary>
public interface IBackupJobRepository
{
    Task<BackupJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupJob>> GetByDatabaseAsync(Guid databaseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupJob>> GetByStatusAsync(BackupStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupJob>> GetPendingJobsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<BackupJob>> GetJobsInDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<BackupJob?> GetLatestSuccessfulBackupAsync(Guid databaseId, BackupType backupType, CancellationToken cancellationToken = default);
    Task AddAsync(BackupJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackupJob job, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
