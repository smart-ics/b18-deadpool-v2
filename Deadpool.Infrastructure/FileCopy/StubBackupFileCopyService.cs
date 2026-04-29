using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.FileCopy;

/// <summary>
/// Stub implementation of backup file copy service for testing.
/// Simulates copy without actually writing to network share.
/// </summary>
public sealed class StubBackupFileCopyService : IBackupFileCopyService
{
    private readonly bool _simulateSuccess;
    private readonly TimeSpan _simulatedDelay;

    public StubBackupFileCopyService(bool simulateSuccess = true, TimeSpan? simulatedDelay = null)
    {
        _simulateSuccess = simulateSuccess;
        _simulatedDelay = simulatedDelay ?? TimeSpan.FromMilliseconds(50);
    }

    public async Task<string> CopyBackupFileAsync(
        string sourceFilePath,
        string databaseName,
        BackupType backupType)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path cannot be empty.", nameof(sourceFilePath));

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source backup file not found: {sourceFilePath}");

        // Simulate copy delay
        await Task.Delay(_simulatedDelay);

        if (!_simulateSuccess)
        {
            throw new IOException("Simulated copy failure");
        }

        // Return simulated destination path
        var fileName = Path.GetFileName(sourceFilePath);
        return Path.Combine("\\\\BackupServer\\Backups", databaseName, fileName);
    }
}
