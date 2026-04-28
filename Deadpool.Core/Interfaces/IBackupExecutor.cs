namespace Deadpool.Core.Interfaces;

public interface IBackupExecutor
{
    Task ExecuteFullBackupAsync(string databaseName, string backupFilePath);
    Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath);
    Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath);
    Task<bool> VerifyBackupFileAsync(string backupFilePath);
}
