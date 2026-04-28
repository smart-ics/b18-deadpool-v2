using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

public class BackupService
{
    private readonly IBackupExecutor _backupExecutor;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly BackupFilePathService _filePathService;

    public BackupService(
        IBackupExecutor backupExecutor,
        IBackupJobRepository backupJobRepository,
        BackupFilePathService filePathService)
    {
        _backupExecutor = backupExecutor ?? throw new ArgumentNullException(nameof(backupExecutor));
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
        _filePathService = filePathService ?? throw new ArgumentNullException(nameof(filePathService));
    }

    public async Task<BackupJob> ExecuteFullBackupAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        var backupFilePath = _filePathService.GenerateBackupFilePath(databaseName, BackupType.Full);
        var backupJob = new BackupJob(databaseName, BackupType.Full, backupFilePath);

        // Persist job BEFORE execution to track even if process crashes
        await _backupJobRepository.CreateAsync(backupJob);

        try
        {
            backupJob.MarkAsRunning();
            await _backupJobRepository.UpdateAsync(backupJob);

            await _backupExecutor.ExecuteFullBackupAsync(databaseName, backupFilePath);

            var fileSize = GetBackupFileSize(backupFilePath);
            backupJob.MarkAsCompleted(fileSize);
            await _backupJobRepository.UpdateAsync(backupJob);
        }
        catch (Exception ex)
        {
            backupJob.MarkAsFailed(ex.Message);
            await _backupJobRepository.UpdateAsync(backupJob);
            throw;
        }

        return backupJob;
    }

    public async Task<bool> VerifyBackupAsync(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        return await _backupExecutor.VerifyBackupFileAsync(backupFilePath);
    }

    private long GetBackupFileSize(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");

        return new FileInfo(backupFilePath).Length;
    }
}
