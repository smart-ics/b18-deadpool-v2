using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Interfaces;

public interface IBackupJobRepository
{
    Task CreateAsync(BackupJob backupJob);
    Task UpdateAsync(BackupJob backupJob);
    Task<BackupJob?> GetByIdAsync(int id);
    Task<IEnumerable<BackupJob>> GetRecentJobsAsync(string databaseName, int count);
    Task<BackupJob?> GetLastSuccessfulFullBackupAsync(string databaseName);
    Task<bool> HasSuccessfulFullBackupAsync(string databaseName);

    // Job claiming for execution worker
    Task<IEnumerable<BackupJob>> GetPendingJobsAsync(int maxCount);
    Task<IEnumerable<BackupJob>> GetPendingOrStaleJobsAsync(int maxCount, TimeSpan staleThreshold);
    Task<bool> TryClaimJobAsync(BackupJob job);
}
