using Deadpool.Agent.Configuration;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Microsoft.Extensions.Options;

namespace Deadpool.Agent.Workers;

// Polls for pending backup jobs and executes them.
// Execution flow:
// 1. Query pending jobs (including stale Running jobs for recovery)
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
    private readonly IBackupFileCopyService? _copyService;
    private readonly bool _copyEnabled;
    private readonly TimeSpan _staleJobThreshold;

    public BackupExecutionWorker(
        ILogger<BackupExecutionWorker> logger,
        IBackupJobRepository jobRepository,
        IBackupExecutor backupExecutor,
        BackupFilePathService filePathService,
        IDatabaseMetadataService databaseMetadataService,
        IOptions<ExecutionWorkerOptions> options,
        IOptions<BackupCopyOptions> copyOptions,
        IBackupFileCopyService? copyService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _backupExecutor = backupExecutor ?? throw new ArgumentNullException(nameof(backupExecutor));
        _filePathService = filePathService ?? throw new ArgumentNullException(nameof(filePathService));
        _databaseMetadataService = databaseMetadataService ?? throw new ArgumentNullException(nameof(databaseMetadataService));

        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _staleJobThreshold = opts.StaleJobThreshold;

        var copyOpts = copyOptions?.Value ?? throw new ArgumentNullException(nameof(copyOptions));
        _copyEnabled = copyOpts.Enabled;
        _copyService = copyService;

        if (_copyEnabled && _copyService == null)
            throw new InvalidOperationException("Copy is enabled but IBackupFileCopyService is not registered.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BackupExecutionWorker started. Stale job threshold: {Threshold}",
            _staleJobThreshold);

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
        // Query pending jobs AND stale Running jobs (for crash recovery)
        var pendingJobs = await _jobRepository.GetPendingOrStaleJobsAsync(maxCount: 10, _staleJobThreshold);

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
            // Attempt to claim the job (transition Pending → Running, or reclaim stale Running)
            var claimed = await _jobRepository.TryClaimJobAsync(job);
            if (!claimed)
            {
                _logger.LogDebug(
                    "Job already claimed by another worker: {Database} {Type}",
                    job.DatabaseName, job.BackupType);
                return;
            }

            // Log if this is a recovery of a stale job
            if (job.Status == BackupStatus.Running)
            {
                _logger.LogWarning(
                    "Recovering stale Running job for {Database} {Type} (started {StartTime:u}, age {Age})",
                    job.DatabaseName, job.BackupType, job.StartTime, DateTime.UtcNow - job.StartTime);
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

            // Copy to remote storage (if enabled)
            if (_copyEnabled && _copyService != null)
            {
                await TryCopyBackupFileAsync(job, backupFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute {Type} backup for {Database}: {Message}",
                job.BackupType, job.DatabaseName, ex.Message);

            // Attempt to mark job as failed
            try
            {
                job.MarkAsFailed(ex.Message);
                await _jobRepository.UpdateAsync(job);
            }
            catch (Exception updateEx)
            {
                // CRITICAL: Failed to update job status after execution failure.
                // Job may be stuck in Running state. Requires operator intervention.
                _logger.LogCritical(updateEx,
                    "CRITICAL: Failed to mark job as Failed for {Database} {Type}. " +
                    "Job may be stuck in Running state. Original error: {OriginalError}",
                    job.DatabaseName, job.BackupType, ex.Message);

                // Job will be recovered on next poll cycle via stale job detection,
                // but operator should be alerted to investigate root cause.
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

    private async Task TryCopyBackupFileAsync(BackupJob job, string backupFilePath)
    {
        try
        {
            _logger.LogInformation(
                "Starting backup file copy for {Database} {Type}",
                job.DatabaseName, job.BackupType);

            // Determine destination path
            var destinationPath = await _copyService!.CopyBackupFileAsync(
                backupFilePath,
                job.DatabaseName,
                job.BackupType);

            job.MarkCopyStarted(destinationPath);
            job.MarkCopyCompleted();
            await _jobRepository.UpdateAsync(job);

            _logger.LogInformation(
                "Backup file copied successfully: {Destination} (duration: {Duration})",
                destinationPath, job.GetCopyDuration());
        }
        catch (Exception ex)
        {
            // Copy failure does NOT endanger the original backup
            _logger.LogError(ex,
                "Failed to copy backup file for {Database} {Type}: {Message}. " +
                "Local backup remains intact at {Path}",
                job.DatabaseName, job.BackupType, ex.Message, backupFilePath);

            try
            {
                job.MarkCopyStarted(backupFilePath); // Record attempt even if failed
                job.MarkCopyFailed(ex.Message);
                await _jobRepository.UpdateAsync(job);
            }
            catch (Exception updateEx)
            {
                _logger.LogWarning(updateEx,
                    "Failed to update copy failure status for {Database} {Type}",
                    job.DatabaseName, job.BackupType);
            }

            // Do not propagate exception - local backup succeeded
        }
    }
}
