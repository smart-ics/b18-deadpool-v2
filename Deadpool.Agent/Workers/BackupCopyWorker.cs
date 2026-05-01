using Deadpool.Agent.Configuration;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.FileCopy;
using Microsoft.Extensions.Options;

namespace Deadpool.Agent.Workers;

public sealed class BackupCopyWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<BackupCopyWorker> _logger;
    private readonly IBackupJobRepository _jobRepository;
    private readonly IBackupFileCopyService _fileCopyService;
    private readonly IOptions<List<DatabaseBackupPolicyOptions>> _policyOptions;

    private readonly HashSet<string> _copiedFiles = new(StringComparer.OrdinalIgnoreCase);

    public BackupCopyWorker(
        ILogger<BackupCopyWorker> logger,
        IBackupJobRepository jobRepository,
        IBackupFileCopyService fileCopyService,
        IOptions<List<DatabaseBackupPolicyOptions>> policyOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _fileCopyService = fileCopyService ?? throw new ArgumentNullException(nameof(fileCopyService));
        _policyOptions = policyOptions ?? throw new ArgumentNullException(nameof(policyOptions));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuredDatabases = _policyOptions.Value
            .Select(p => p.DatabaseName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "BackupCopyWorker started. Monitoring {DatabaseCount} databases for completed backups.",
            configuredDatabases.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCompletedBackupsAsync(configuredDatabases, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in backup copy worker loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("BackupCopyWorker stopped.");
    }

    private async Task ProcessCompletedBackupsAsync(
        IReadOnlyCollection<string> configuredDatabases,
        CancellationToken cancellationToken)
    {
        foreach (var databaseName in configuredDatabases)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var backups = await _jobRepository.GetBackupsByDatabaseAsync(databaseName);
            var completedJobs = backups
                .Where(b => b.Status == BackupStatus.Completed)
                .OrderBy(b => b.StartTime)
                .ToList();

            foreach (var job in completedJobs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (!_copiedFiles.Add(job.BackupFilePath))
                    continue;

                try
                {
                    var exists = File.Exists(job.BackupFilePath);
                    if (!exists)
                    {
                        _logger.LogWarning(
                            "Completed backup file not found on staging for {Database}. File: {FilePath}",
                            job.DatabaseName,
                            job.BackupFilePath);
                        _copiedFiles.Remove(job.BackupFilePath);
                        continue;
                    }

                    _logger.LogInformation(
                        "Detected completed backup file for copy. Database: {Database}. File: {FilePath}",
                        job.DatabaseName,
                        job.BackupFilePath);

                    _logger.LogInformation(
                        "Executing copy for completed backup. Database: {Database}. File: {FilePath}",
                        job.DatabaseName,
                        job.BackupFilePath);

                    await _fileCopyService.CopyBackupFileAsync(
                        job.BackupFilePath,
                        job.DatabaseName,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _copiedFiles.Remove(job.BackupFilePath);
                    _logger.LogError(
                        ex,
                        "Copy failed for completed backup. Database: {Database}. File: {FilePath}",
                        job.DatabaseName,
                        job.BackupFilePath);
                }
            }
        }
    }
}
