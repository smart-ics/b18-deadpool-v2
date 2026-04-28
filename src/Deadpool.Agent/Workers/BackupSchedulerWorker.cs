using Deadpool.Core.Application.Services;
using Deadpool.Core.Domain.Repositories;

namespace Deadpool.Agent.Workers;

/// <summary>
/// Worker service that monitors backup schedules and creates pending jobs
/// </summary>
public class BackupSchedulerWorker : BackgroundService
{
    private readonly ILogger<BackupSchedulerWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public BackupSchedulerWorker(
        ILogger<BackupSchedulerWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup Scheduler Worker starting at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Backup Scheduler Worker: {Message}", ex.Message);
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Backup Scheduler Worker stopping at: {time}", DateTimeOffset.Now);
    }

    private async Task ProcessSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var scheduleRepo = scope.ServiceProvider.GetRequiredService<IBackupScheduleRepository>();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IBackupJobRepository>();
        var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();

        var dueSchedules = await schedulerService.GetDueSchedulesAsync(cancellationToken);

        foreach (var schedule in dueSchedules)
        {
            try
            {
                _logger.LogInformation(
                    "Creating backup job for schedule: {ScheduleName} (Database: {DatabaseId})",
                    schedule.Name,
                    schedule.DatabaseId);

                // Create backup job
                var job = new Core.Domain.Entities.BackupJob(
                    schedule.DatabaseId,
                    schedule.BackupType,
                    schedule.GenerateBackupFilePath("DatabaseName", DateTime.UtcNow),
                    DateTime.UtcNow,
                    schedule.Id,
                    schedule.IsCompressed,
                    schedule.IsEncrypted);

                await jobRepo.AddAsync(job, cancellationToken);

                // Calculate next run time
                var nextRun = schedulerService.CalculateNextRunTime(schedule.CronExpression, DateTime.UtcNow);
                schedule.RecordExecution(job.Id, DateTime.UtcNow, nextRun);
                await scheduleRepo.UpdateAsync(schedule, cancellationToken);

                _logger.LogInformation(
                    "Created backup job {JobId} for schedule {ScheduleName}. Next run: {NextRun}",
                    job.Id,
                    schedule.Name,
                    nextRun);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating backup job for schedule {ScheduleId}: {Message}",
                    schedule.Id,
                    ex.Message);
            }
        }
    }
}
