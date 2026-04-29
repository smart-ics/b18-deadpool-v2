using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

public interface IBackupExecutor
{
    Task ExecuteFullBackupAsync(string databaseName, string backupFilePath);
    Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath);
    Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath);
    Task<bool> VerifyBackupFileAsync(string backupFilePath);

    /// <summary>
    /// Retrieves LSN metadata for a backup from msdb.dbo.backupset.
    /// Returns null if metadata cannot be retrieved.
    /// Used for restore chain validation and retention cleanup safety.
    /// </summary>
    Task<BackupLSNMetadata?> GetBackupLSNMetadataAsync(string databaseName, string backupFilePath);
}
