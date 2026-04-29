using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.BackupExecution;

// Stub implementation for testing the worker pipeline without SQL Server.
// Simulates backup execution with a small delay.
public sealed class StubBackupExecutor : IBackupExecutor
{
    public async Task ExecuteFullBackupAsync(string databaseName, string backupFilePath)
    {
        ValidateParameters(databaseName, backupFilePath);
        await SimulateBackupAsync();
        CreateStubFile(backupFilePath);
    }

    public async Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath)
    {
        ValidateParameters(databaseName, backupFilePath);
        await SimulateBackupAsync();
        CreateStubFile(backupFilePath);
    }

    public async Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath)
    {
        ValidateParameters(databaseName, backupFilePath);
        await SimulateBackupAsync();
        CreateStubFile(backupFilePath);
    }

    public Task<bool> VerifyBackupFileAsync(string backupFilePath)
    {
        return Task.FromResult(File.Exists(backupFilePath));
    }

    private static void ValidateParameters(string databaseName, string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));
    }

    private static async Task SimulateBackupAsync()
    {
        // Simulate backup work
        await Task.Delay(TimeSpan.FromMilliseconds(100));
    }

    private static void CreateStubFile(string backupFilePath)
    {
        var directory = Path.GetDirectoryName(backupFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create empty file to simulate backup
        File.WriteAllText(backupFilePath, "STUB BACKUP FILE");
    }
}
