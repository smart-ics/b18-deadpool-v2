using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Persistence;

// In-memory implementation of IBackupJobRepository for single-node worker pipeline.
// Thread-safe for concurrent access by scheduler and executor workers.
public sealed class InMemoryBackupJobRepository : IBackupJobRepository
{
    private readonly List<BackupJob> _jobs = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task CreateAsync(BackupJob backupJob)
    {
        if (backupJob == null)
            throw new ArgumentNullException(nameof(backupJob));

        lock (_lock)
        {
            _jobs.Add(backupJob);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(BackupJob backupJob)
    {
        if (backupJob == null)
            throw new ArgumentNullException(nameof(backupJob));

        // In-memory: job is already updated by reference
        return Task.CompletedTask;
    }

    public Task<BackupJob?> GetByIdAsync(int id)
    {
        lock (_lock)
        {
            return Task.FromResult(_jobs.FirstOrDefault(j => GetJobId(j) == id));
        }
    }

    public Task<IEnumerable<BackupJob>> GetRecentJobsAsync(string databaseName, int count)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var recent = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .OrderByDescending(j => j.StartTime)
                .Take(count)
                .ToList();

            return Task.FromResult<IEnumerable<BackupJob>>(recent);
        }
    }

    public Task<BackupJob?> GetLastSuccessfulFullBackupAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var lastFull = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.BackupType == BackupType.Full)
                .Where(j => j.Status == BackupStatus.Completed)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefault();

            return Task.FromResult(lastFull);
        }
    }

    public Task<bool> HasSuccessfulFullBackupAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var hasFullBackup = _jobs
                .Any(j => j.DatabaseName == databaseName &&
                          j.BackupType == BackupType.Full &&
                          j.Status == BackupStatus.Completed);

            return Task.FromResult(hasFullBackup);
        }
    }

    public Task<IEnumerable<BackupJob>> GetPendingJobsAsync(int maxCount)
    {
        lock (_lock)
        {
            var pending = _jobs
                .Where(j => j.Status == BackupStatus.Pending)
                .OrderBy(j => j.StartTime)
                .Take(maxCount)
                .ToList();

            return Task.FromResult<IEnumerable<BackupJob>>(pending);
        }
    }

    public Task<bool> TryClaimJobAsync(BackupJob job)
    {
        if (job == null)
            throw new ArgumentNullException(nameof(job));

        lock (_lock)
        {
            // Check if job is still pending (another worker might have claimed it)
            if (job.Status != BackupStatus.Pending)
                return Task.FromResult(false);

            try
            {
                job.MarkAsRunning();
                return Task.FromResult(true);
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(false);
            }
        }
    }

    public Task<BackupJob?> GetLastSuccessfulBackupAsync(string databaseName, BackupType backupType)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var lastBackup = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.BackupType == backupType)
                .Where(j => j.Status == BackupStatus.Completed)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefault();

            return Task.FromResult(lastBackup);
        }
    }

    public Task<BackupJob?> GetLastFailedBackupAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var lastFailed = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.Status == BackupStatus.Failed)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefault();

            return Task.FromResult(lastFailed);
        }
    }

    public Task<IEnumerable<BackupJob>> GetBackupChainAsync(string databaseName, DateTime since)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        lock (_lock)
        {
            var chain = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.Status == BackupStatus.Completed)
                .Where(j => j.StartTime >= since)
                .OrderBy(j => j.StartTime)
                .ToList();

            return Task.FromResult<IEnumerable<BackupJob>>(chain);
        }
    }

    // Helper for testing - not part of interface
    private int GetJobId(BackupJob job)
    {
        return _jobs.IndexOf(job) + 1;
    }

    // Helper for testing - clear all jobs
    public void Clear()
    {
        lock (_lock)
        {
            _jobs.Clear();
            _nextId = 1;
        }
    }
}
