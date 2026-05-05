using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

public interface IBackupProgressService
{
    Task<BackupProgress?> GetRunningBackupAsync();
}
