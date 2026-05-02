using Deadpool.Core.Domain.ValueObjects;
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

    public Task<BackupLSNMetadata?> GetBackupLSNMetadataAsync(string databaseName, string backupFilePath)
    {
        ValidateParameters(databaseName, backupFilePath);

        if (backupFilePath.Contains("_FULL_", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                firstLSN: null,
                lastLSN: null,
                databaseBackupLSN: 1000m,
                checkpointLSN: 1000m));
        }

        if (backupFilePath.Contains("_DIFF_", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                firstLSN: null,
                lastLSN: null,
                databaseBackupLSN: 1000m,
                checkpointLSN: null));
        }

        if (backupFilePath.Contains("_LOG_", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                firstLSN: 2000m,
                lastLSN: 3000m,
                databaseBackupLSN: 1000m,
                checkpointLSN: null));
        }

        throw new InvalidOperationException($"Cannot infer backup type from path: {backupFilePath}");
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
