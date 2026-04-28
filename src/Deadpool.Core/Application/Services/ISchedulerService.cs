using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Application.Services;

/// <summary>
/// Service for managing backup schedules and calculating next run times
/// </summary>
public interface ISchedulerService
{
    /// <summary>
    /// Calculate next run time based on cron expression
    /// </summary>
    DateTime? CalculateNextRunTime(string cronExpression, DateTime fromTime);

    /// <summary>
    /// Get all schedules due for execution
    /// </summary>
    Task<IEnumerable<BackupSchedule>> GetDueSchedulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate cron expression
    /// </summary>
    bool ValidateCronExpression(string cronExpression);

    /// <summary>
    /// Get human-readable description of cron schedule
    /// </summary>
    string GetScheduleDescription(string cronExpression);
}
