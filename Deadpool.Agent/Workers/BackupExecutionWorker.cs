using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
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
            var jobId = BuildJobId(job);

            // Attempt to claim the job (transition Pending → Running)
            var claimed = await _jobRepository.TryClaimJobAsync(job);
            if (!claimed)
            {
                _logger.LogDebug(
                    "Job already claimed by another worker: {Database} {Type} ({JobId})",
                    job.DatabaseName, job.BackupType, jobId);
                return;
            }

            if (job.Status == BackupStatus.Pending)
            {
                job.MarkAsRunning();
                await _jobRepository.UpdateAsync(job);
            }
            else if (job.Status != BackupStatus.Running)
            {
                _logger.LogWarning(
                    "Skipping execution for {Database} {Type}. Invalid job status: {Status} ({JobId})",
                    job.DatabaseName,
                    job.BackupType,
                    job.Status,
                    jobId);
                return;
            }

            _logger.LogInformation(
                "Executing {Type} backup -> {JobId}",
                job.BackupType, jobId);

            // Generate proper file path (replace scheduler's placeholder)
            var actualBackupFilePath = _filePathService.GenerateBackupFilePath(job.DatabaseName, job.BackupType);
            ValidateRealBackupFilePath(actualBackupFilePath);

            // Validate prerequisites
            await ValidatePrerequisitesAsync(job.DatabaseName, job.BackupType);

            // Execute backup directly
            await ExecuteBackupAsync(job.DatabaseName, job.BackupType, actualBackupFilePath);

            _logger.LogInformation("Backup file created: {Path}", actualBackupFilePath);

            // Extract required LSN metadata from backup header before completion.
            var lsnMetadata = await _backupExecutor.GetBackupLSNMetadataAsync(job.DatabaseName, actualBackupFilePath)
                ?? throw new InvalidOperationException(
                    $"LSN metadata extraction returned no data for '{job.DatabaseName}' ({job.BackupType}).");

            var mappedLsn = MapAndValidateRequiredLsn(job.BackupType, lsnMetadata);

            // Verify backup before marking completion
            var isVerified = await _backupExecutor.VerifyBackupFileAsync(actualBackupFilePath);
            if (!isVerified)
                throw new InvalidOperationException(
                    $"Backup verification failed for database '{job.DatabaseName}' ({job.BackupType}).");

            // Get file size and mark completed
            EnsureReadyToComplete(job, actualBackupFilePath);

            _logger.LogInformation("Marking job completed with path: {Path}", actualBackupFilePath);

            var fileSize = GetBackupFileSize(actualBackupFilePath);
            job.MarkAsCompleted(actualBackupFilePath, fileSize);
            job.SetLSNMetadata(
                mappedLsn.FirstLSN,
                mappedLsn.LastLSN,
                mappedLsn.DatabaseBackupLSN,
                mappedLsn.CheckpointLSN);
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

    private static void EnsureReadyToComplete(BackupJob job, string backupFilePath)
    {
        if (job.Status != BackupStatus.Running)
            throw new InvalidOperationException(
                $"Cannot complete backup job for '{job.DatabaseName}' ({job.BackupType}) while status is {job.Status}.");

        ValidateRealBackupFilePath(backupFilePath);
    }

    private static void ValidateRealBackupFilePath(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        if (Path.GetFileName(backupFilePath).StartsWith("PENDING_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Backup file path is still pending placeholder: {backupFilePath}");

        if (!Path.IsPathRooted(backupFilePath))
            throw new InvalidOperationException($"Backup file path must be an absolute path: {backupFilePath}");
    }

    private static string BuildJobId(BackupJob job)
    {
        return $"{job.DatabaseName}:{job.BackupType}:{job.StartTime:O}";
    }

    private static BackupLSNMetadata MapAndValidateRequiredLsn(BackupType backupType, BackupLSNMetadata header)
    {
        return backupType switch
        {
            BackupType.Full => new BackupLSNMetadata(
                firstLSN: null,
                lastLSN: null,
                databaseBackupLSN: RequireLsn(header.DatabaseBackupLSN, backupType, nameof(header.DatabaseBackupLSN)),
                checkpointLSN: RequireLsn(header.CheckpointLSN, backupType, nameof(header.CheckpointLSN))),

            BackupType.Differential => new BackupLSNMetadata(
                firstLSN: null,
                lastLSN: null,
                databaseBackupLSN: RequireLsn(header.DatabaseBackupLSN, backupType, nameof(header.DatabaseBackupLSN)),
                checkpointLSN: null),

            BackupType.TransactionLog => new BackupLSNMetadata(
                firstLSN: RequireLsn(header.FirstLSN, backupType, nameof(header.FirstLSN)),
                lastLSN: RequireLsn(header.LastLSN, backupType, nameof(header.LastLSN)),
                databaseBackupLSN: RequireLsn(header.DatabaseBackupLSN, backupType, nameof(header.DatabaseBackupLSN)),
                checkpointLSN: null),

            _ => throw new InvalidOperationException($"Unknown backup type: {backupType}")
        };
    }

    private static decimal RequireLsn(decimal? value, BackupType backupType, string fieldName)
    {
        if (!value.HasValue)
            throw new InvalidOperationException(
                $"Backup metadata incomplete for {backupType}: missing required {fieldName}.");

        return value.Value;
    }
}
