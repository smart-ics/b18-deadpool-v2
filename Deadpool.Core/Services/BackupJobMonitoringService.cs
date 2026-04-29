using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

/// <summary>
/// Service for backup job monitoring and history queries.
/// </summary>
public class BackupJobMonitoringService : IBackupJobMonitoringService
{
    private readonly IBackupJobRepository _repository;

    public BackupJobMonitoringService(IBackupJobRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<BackupJobDisplayModel>> GetBackupJobHistoryAsync(BackupJobFilter filter)
    {
        // Get all jobs for database
        var jobs = await _repository.GetBackupsByDatabaseAsync(filter.DatabaseName ?? "");

        // Apply filters
        var filtered = jobs.AsEnumerable();

        if (filter.BackupType.HasValue)
        {
            filtered = filtered.Where(j => j.BackupType == filter.BackupType.Value);
        }

        if (filter.Status.HasValue)
        {
            filtered = filtered.Where(j => j.Status == filter.Status.Value);
        }

        if (filter.StartDate.HasValue)
        {
            filtered = filtered.Where(j => j.StartTime >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            // End date is inclusive (end of day)
            var endOfDay = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            filtered = filtered.Where(j => j.StartTime <= endOfDay);
        }

        // Order by newest first
        var ordered = filtered
            .OrderByDescending(j => j.StartTime)
            .Take(filter.MaxResults);

        return ordered.Select(j => new BackupJobDisplayModel(j)).ToList();
    }

    public async Task<Dictionary<string, int>> GetJobStatusSummaryAsync(string databaseName)
    {
        var jobs = await _repository.GetBackupsByDatabaseAsync(databaseName);

        var summary = new Dictionary<string, int>
        {
            ["Pending"] = jobs.Count(j => j.Status == BackupStatus.Pending),
            ["Running"] = jobs.Count(j => j.Status == BackupStatus.Running),
            ["Completed"] = jobs.Count(j => j.Status == BackupStatus.Completed),
            ["Failed"] = jobs.Count(j => j.Status == BackupStatus.Failed)
        };

        return summary;
    }
}
