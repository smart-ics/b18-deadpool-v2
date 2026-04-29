using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;

namespace Deadpool.Agent.Workers;

// Polls for pending backup jobs and executes them.
// Execution flow:
// 1. Query pending jobs
// 2. Claim job (mark as Running)
// 3. Execute backup directly via IBackupExecutor
// 4. Update job status (Completed or Failed)
public sealed class BackupExecutionWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<BackupExecutionWorker> _logger;
    private readonly IBackupJobRepository _jobRepository;
    private readonly IBackupExecutor _backupExecutor;
    private readonly BackupFilePathService _filePathService;
    private readonly IDatabaseMetadataService _databaseMetadataService;

    public BackupExecutionWorker(
        ILogger<BackupExecutionWorker> logger,
        IBackupJobRepository jobRepository,
        IBackupExecutor backupExecutor,
        BackupFilePathService filePathService,
        IDatabaseMetadataService databaseMetadataService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _backupExecutor = backupExecutor ?? throw new ArgumentNullException(nameof(backupExecutor));
        _filePathService = filePathService ?? throw new ArgumentNullException(nameof(filePathService));
        _databaseMetadataService = databaseMetadataService ?? throw new ArgumentNullException(nameof(databaseMetadataService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackupExecutionWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in execution worker loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("BackupExecutionWorker stopped.");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        var pendingJobs = await _jobRepository.GetPendingJobsAsync(maxCount: 10);

        foreach (var job in pendingJobs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await TryExecuteJobAsync(job, cancellationToken);
        }
    }

    private async Task TryExecuteJobAsync(BackupJob job, CancellationToken cancellationToken)
    {
        try
        {
            // Attempt to claim the job (transition Pending → Running)
            var claimed = await _jobRepository.TryClaimJobAsync(job);
            if (!claimed)
            {
                _logger.LogDebug(
                    "Job already claimed by another worker: {Database} {Type}",
                    job.DatabaseName, job.BackupType);
                return;
            }

            _logger.LogInformation(
                "Executing {Type} backup for {Database}",
                job.BackupType, job.DatabaseName);

            // Generate proper file path (replace scheduler's placeholder)
            var backupFilePath = _filePathService.GenerateBackupFilePath(job.DatabaseName, job.BackupType);

            // Validate prerequisites
            await ValidatePrerequisitesAsync(job.DatabaseName, job.BackupType);

            // Execute backup directly
            await ExecuteBackupAsync(job.DatabaseName, job.BackupType, backupFilePath);

            // Get file size and mark completed
            var fileSize = GetBackupFileSize(backupFilePath);
            job.MarkAsCompleted(fileSize);
            await _jobRepository.UpdateAsync(job);

            _logger.LogInformation(
                "Successfully completed {Type} backup for {Database}",
                job.BackupType, job.DatabaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute {Type} backup for {Database}: {Message}",
                job.BackupType, job.DatabaseName, ex.Message);

            try
            {
                job.MarkAsFailed(ex.Message);
                await _jobRepository.UpdateAsync(job);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx,
                    "Failed to update job status to Failed for {Database} {Type}",
                    job.DatabaseName, job.BackupType);
            }
        }
    }

    private async Task ValidatePrerequisitesAsync(string databaseName, BackupType backupType)
    {
        if (backupType == BackupType.Differential)
        {
            var hasFullBackup = await _jobRepository.HasSuccessfulFullBackupAsync(databaseName);
            if (!hasFullBackup)
                throw new InvalidOperationException(
                    $"Cannot execute differential backup for database '{databaseName}'. " +
                    "No successful full backup found.");
        }

        if (backupType == BackupType.TransactionLog)
        {
            var recoveryModel = await _databaseMetadataService.GetRecoveryModelAsync(databaseName);
            if (recoveryModel == RecoveryModel.Simple)
                throw new InvalidOperationException(
                    $"Cannot execute transaction log backup for database '{databaseName}'. " +
                    "Database is in SIMPLE recovery model.");

            var hasFullBackup = await _jobRepository.HasSuccessfulFullBackupAsync(databaseName);
            if (!hasFullBackup)
                throw new InvalidOperationException(
                    $"Cannot execute transaction log backup for database '{databaseName}'. " +
                    "No successful full backup found.");
        }
    }

    private async Task ExecuteBackupAsync(string databaseName, BackupType backupType, string backupFilePath)
    {
        switch (backupType)
        {
            case BackupType.Full:
                await _backupExecutor.ExecuteFullBackupAsync(databaseName, backupFilePath);
                break;

            case BackupType.Differential:
                await _backupExecutor.ExecuteDifferentialBackupAsync(databaseName, backupFilePath);
                break;

            case BackupType.TransactionLog:
                await _backupExecutor.ExecuteTransactionLogBackupAsync(databaseName, backupFilePath);
                break;

            default:
                throw new InvalidOperationException($"Unknown backup type: {backupType}");
        }
    }

    private long GetBackupFileSize(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");

        return new FileInfo(backupFilePath).Length;
    }
}
