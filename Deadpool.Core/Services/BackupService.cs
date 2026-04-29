using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

public class BackupService
{
    private readonly IBackupExecutor _backupExecutor;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly BackupFilePathService _filePathService;
    private readonly IDatabaseMetadataService _databaseMetadataService;

    public BackupService(
        IBackupExecutor backupExecutor,
        IBackupJobRepository backupJobRepository,
        BackupFilePathService filePathService,
        IDatabaseMetadataService databaseMetadataService)
    {
        _backupExecutor = backupExecutor ?? throw new ArgumentNullException(nameof(backupExecutor));
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
        _filePathService = filePathService ?? throw new ArgumentNullException(nameof(filePathService));
        _databaseMetadataService = databaseMetadataService ?? throw new ArgumentNullException(nameof(databaseMetadataService));
    }

    public async Task<BackupJob> ExecuteFullBackupAsync(string databaseName)
    {
        return await ExecuteBackupAsync(
            databaseName,
            BackupType.Full,
            _backupExecutor.ExecuteFullBackupAsync,
            prerequisiteValidator: null);
    }

    public async Task<BackupJob> ExecuteDifferentialBackupAsync(string databaseName)
    {
        return await ExecuteBackupAsync(
            databaseName,
            BackupType.Differential,
            _backupExecutor.ExecuteDifferentialBackupAsync,
            ValidateDifferentialPrerequisites);
    }

    public async Task<BackupJob> ExecuteTransactionLogBackupAsync(string databaseName)
    {
        return await ExecuteBackupAsync(
            databaseName,
            BackupType.TransactionLog,
            _backupExecutor.ExecuteTransactionLogBackupAsync,
            ValidateTransactionLogPrerequisites);
    }

    public async Task<bool> VerifyBackupAsync(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        return await _backupExecutor.VerifyBackupFileAsync(backupFilePath);
    }

    // Template method: Common backup execution lifecycle
    private async Task<BackupJob> ExecuteBackupAsync(
        string databaseName,
        BackupType backupType,
        Func<string, string, Task> backupExecutor,
        Func<string, Task>? prerequisiteValidator)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        // Optional domain-specific prerequisite validation
        if (prerequisiteValidator != null)
            await prerequisiteValidator(databaseName);

        var backupFilePath = _filePathService.GenerateBackupFilePath(databaseName, backupType);
        var backupJob = new BackupJob(databaseName, backupType, backupFilePath);

        // Persist job BEFORE execution to track even if process crashes
        await _backupJobRepository.CreateAsync(backupJob);

        try
        {
            backupJob.MarkAsRunning();
            await _backupJobRepository.UpdateAsync(backupJob);

            await backupExecutor(databaseName, backupFilePath);

            var fileSize = GetBackupFileSize(backupFilePath);
            backupJob.MarkAsCompleted(fileSize);

            // Capture LSN metadata from SQL Server for restore chain validation
            await CaptureAndSetLSNMetadataAsync(backupJob);

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

    /// <summary>
    /// Captures LSN metadata from msdb.dbo.backupset for restore chain validation.
    /// Failure to capture LSN metadata is logged but does not fail the backup job.
    /// Conservative: If LSN capture fails, retention cleanup will retain more backups.
    /// </summary>
    private async Task CaptureAndSetLSNMetadataAsync(BackupJob backupJob)
    {
        try
        {
            var lsnMetadata = await _backupExecutor.GetBackupLSNMetadataAsync(
                backupJob.DatabaseName,
                backupJob.BackupFilePath);

            if (lsnMetadata != null)
            {
                backupJob.SetLSNMetadata(
                    lsnMetadata.FirstLSN,
                    lsnMetadata.LastLSN,
                    lsnMetadata.DatabaseBackupLSN,
                    lsnMetadata.CheckpointLSN);
            }
        }
        catch
        {
            // LSN capture failure is non-fatal
            // Conservative: Backups without LSN metadata will be retained longer by retention cleanup
            // This prevents accidental deletion of backups that may be needed for restore chains
        }
    }

    // Domain-specific prerequisite validation for differential backups
    private async Task ValidateDifferentialPrerequisites(string databaseName)
    {
        var hasFullBackup = await _backupJobRepository.HasSuccessfulFullBackupAsync(databaseName);
        if (!hasFullBackup)
            throw new InvalidOperationException(
                $"Cannot execute differential backup for database '{databaseName}'. " +
                "No successful full backup found. A full backup is required as the differential base.");
    }

    // Domain-specific prerequisite validation for transaction log backups
    private async Task ValidateTransactionLogPrerequisites(string databaseName)
    {
        var recoveryModel = await _databaseMetadataService.GetRecoveryModelAsync(databaseName);
        if (recoveryModel == RecoveryModel.Simple)
            throw new InvalidOperationException(
                $"Cannot execute transaction log backup for database '{databaseName}'. " +
                "Database is in SIMPLE recovery model. Transaction log backups require FULL or BULK_LOGGED recovery model.");

        var hasFullBackup = await _backupJobRepository.HasSuccessfulFullBackupAsync(databaseName);
        if (!hasFullBackup)
            throw new InvalidOperationException(
                $"Cannot execute transaction log backup for database '{databaseName}'. " +
                "No successful full backup found. A full backup is required to establish the log backup chain.");
    }

    private long GetBackupFileSize(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");

        return new FileInfo(backupFilePath).Length;
    }
}
