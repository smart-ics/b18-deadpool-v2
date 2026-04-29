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
    Task<bool> TryClaimJobAsync(BackupJob job);

    // Health monitoring queries
    Task<BackupJob?> GetLastSuccessfulBackupAsync(string databaseName, BackupType backupType);
    Task<BackupJob?> GetLastFailedBackupAsync(string databaseName);
    Task<IEnumerable<BackupJob>> GetBackupChainAsync(string databaseName, DateTime since);
}
