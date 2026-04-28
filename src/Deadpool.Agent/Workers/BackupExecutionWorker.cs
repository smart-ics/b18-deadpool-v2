using Deadpool.Core.Application.Services;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.Repositories;

namespace Deadpool.Agent.Workers;

/// <summary>
/// Worker service that executes pending backup jobs
/// </summary>
public class BackupExecutionWorker : BackgroundService
{
    private readonly ILogger<BackupExecutionWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private readonly int _maxConcurrentJobs = 3;

    public BackupExecutionWorker(
        ILogger<BackupExecutionWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup Execution Worker starting at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Backup Execution Worker: {Message}", ex.Message);
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Backup Execution Worker stopping at: {time}", DateTimeOffset.Now);
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IBackupJobRepository>();
        var databaseRepo = scope.ServiceProvider.GetRequiredService<IDatabaseRepository>();
        var serverRepo = scope.ServiceProvider.GetRequiredService<ISqlServerInstanceRepository>();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupExecutionService>();

        var pendingJobs = await jobRepo.GetPendingJobsAsync(cancellationToken);
        var jobsToProcess = pendingJobs.Take(_maxConcurrentJobs).ToList();

        var tasks = jobsToProcess.Select(job => ExecuteJobAsync(
            job, 
            databaseRepo, 
            serverRepo, 
            jobRepo, 
            backupService, 
            cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteJobAsync(
        Core.Domain.Entities.BackupJob job,
        IDatabaseRepository databaseRepo,
        ISqlServerInstanceRepository serverRepo,
        IBackupJobRepository jobRepo,
        IBackupExecutionService backupService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get database and server info
            var database = await databaseRepo.GetByIdAsync(job.DatabaseId, cancellationToken);
            if (database == null)
            {
                _logger.LogError("Database {DatabaseId} not found for job {JobId}", job.DatabaseId, job.Id);
                job.Fail("Database not found");
                await jobRepo.UpdateAsync(job, cancellationToken);
                return;
            }

            var server = await serverRepo.GetByIdAsync(database.SqlServerInstanceId, cancellationToken);
            if (server == null)
            {
                _logger.LogError("Server {ServerId} not found for job {JobId}", database.SqlServerInstanceId, job.Id);
                job.Fail("Server not found");
                await jobRepo.UpdateAsync(job, cancellationToken);
                return;
            }

            _logger.LogInformation(
                "Starting backup job {JobId}: {BackupType} backup of {Database} on {Server}",
                job.Id,
                job.BackupType,
                database.Name,
                server.GetFullServerName());

            // Start the job
            job.Start();
            await jobRepo.UpdateAsync(job, cancellationToken);

            // Execute the backup
            var result = await backupService.ExecuteBackupAsync(job, database, server, cancellationToken);

            if (result.IsSuccess)
            {
                // Get file size
                var fileInfo = new FileInfo(job.BackupFilePath);
                var fileSize = fileInfo.Exists ? fileInfo.Length : 0;

                job.Complete(fileSize);
                database.UpdateLastBackupDate(DateTime.UtcNow, job.BackupType);

                _logger.LogInformation(
                    "Backup job {JobId} completed successfully. Size: {Size} bytes",
                    job.Id,
                    fileSize);
            }
            else
            {
                job.Fail(result.Error);
                _logger.LogError("Backup job {JobId} failed: {Error}", job.Id, result.Error);
            }

            await jobRepo.UpdateAsync(job, cancellationToken);
            await databaseRepo.UpdateAsync(database, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing backup job {JobId}: {Message}", job.Id, ex.Message);
            
            try
            {
                job.Fail($"Unexpected error: {ex.Message}");
                await jobRepo.UpdateAsync(job, cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update job {JobId} after error", job.Id);
            }
        }
    }
}
