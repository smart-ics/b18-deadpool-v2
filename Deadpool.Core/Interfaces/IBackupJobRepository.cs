using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Interfaces;

public interface IBackupJobRepository
{
    Task CreateAsync(BackupJob backupJob);
    Task UpdateAsync(BackupJob backupJob);
    Task<BackupJob?> GetByIdAsync(int id);
    Task<IEnumerable<BackupJob>> GetRecentJobsAsync(string databaseName, int count);
    Task<BackupJob?> GetLastSuccessfulFullBackupAsync(string databaseName);
    Task<bool> HasSuccessfulFullBackupAsync(string databaseName);
}
