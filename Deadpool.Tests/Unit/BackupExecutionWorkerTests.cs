using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace Deadpool.Tests.Unit;

public class BackupExecutionWorkerTests
{
    [Fact]
    public async Task Executor_ShouldSerializeBackups_PerDatabaseAcrossWorkers()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"deadpool-worker-lock-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var repository = new ConcurrentClaimInMemoryBackupJobRepository();
            var backupExecutor = new ConcurrencyTrackingBackupExecutor(delayMs: 250);
            var metadataService = new StubMetadataService();
            var filePathService = new BackupFilePathService(tempRoot);

            var worker1 = new BackupExecutionWorker(
                NullLogger<BackupExecutionWorker>.Instance,
                repository,
                backupExecutor,
                filePathService,
                metadataService);

            var worker2 = new BackupExecutionWorker(
                NullLogger<BackupExecutionWorker>.Instance,
                repository,
                backupExecutor,
                filePathService,
                metadataService);

            var fullJob = new BackupJob("LockDB", BackupType.Full, Path.Combine(tempRoot, "full-placeholder.bak"));
            var logJob = new BackupJob("LockDB", BackupType.TransactionLog, Path.Combine(tempRoot, "log-placeholder.trn"));

            await repository.CreateAsync(fullJob);
            await repository.CreateAsync(logJob);

            using var cts = new CancellationTokenSource();

            var run1 = worker1.StartAsync(cts.Token);
            var run2 = worker2.StartAsync(cts.Token);

            await Task.Delay(1500);

            await cts.CancelAsync();
            await Task.WhenAll(run1, run2);

            fullJob.Status.Should().Be(BackupStatus.Completed);
            logJob.Status.Should().Be(BackupStatus.Completed);
            backupExecutor.GetMaxConcurrency("LockDB").Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Executor_ShouldTransitionLifecycleAndAssignRealFilePath_ForAllBackupTypes()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), $"deadpool-worker-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var repository = new ClaimOnlyInMemoryBackupJobRepository();
            var backupExecutor = new StubFileBackupExecutor();
            var metadataService = new StubMetadataService();
            var filePathService = new BackupFilePathService(tempRoot);

            var worker = new BackupExecutionWorker(
                NullLogger<BackupExecutionWorker>.Instance,
                repository,
                backupExecutor,
                filePathService,
                metadataService);

            var fullJob = new BackupJob("TestDB", BackupType.Full, Path.Combine(tempRoot, "full-placeholder.bak"));
            var diffJob = new BackupJob("TestDB", BackupType.Differential, Path.Combine(tempRoot, "diff-placeholder.bak"));
            var logJob = new BackupJob("TestDB", BackupType.TransactionLog, Path.Combine(tempRoot, "log-placeholder.trn"));

            await repository.CreateAsync(fullJob);
            await repository.CreateAsync(diffJob);
            await repository.CreateAsync(logJob);

            // Act
            var cts = new CancellationTokenSource();
            var workerTask = worker.StartAsync(cts.Token);
            await Task.Delay(1200);
            await cts.CancelAsync();
            await workerTask;

            // Assert
            fullJob.Status.Should().Be(BackupStatus.Completed);
            diffJob.Status.Should().Be(BackupStatus.Completed);
            logJob.Status.Should().Be(BackupStatus.Completed);

            fullJob.DatabaseBackupLSN.Should().NotBeNull();
            fullJob.CheckpointLSN.Should().NotBeNull();

            diffJob.DatabaseBackupLSN.Should().NotBeNull();

            logJob.FirstLSN.Should().NotBeNull();
            logJob.LastLSN.Should().NotBeNull();
            logJob.DatabaseBackupLSN.Should().NotBeNull();

            fullJob.BackupFilePath.Should().NotStartWith("PENDING_");
            diffJob.BackupFilePath.Should().NotStartWith("PENDING_");
            logJob.BackupFilePath.Should().NotStartWith("PENDING_");

            Path.IsPathRooted(fullJob.BackupFilePath).Should().BeTrue();
            Path.IsPathRooted(diffJob.BackupFilePath).Should().BeTrue();
            Path.IsPathRooted(logJob.BackupFilePath).Should().BeTrue();

            File.Exists(fullJob.BackupFilePath).Should().BeTrue();
            File.Exists(diffJob.BackupFilePath).Should().BeTrue();
            File.Exists(logJob.BackupFilePath).Should().BeTrue();

            repository.GetStatusHistory(fullJob)
                .Should().ContainInOrder(BackupStatus.Running, BackupStatus.Completed);

            repository.GetStatusHistory(diffJob)
                .Should().ContainInOrder(BackupStatus.Running, BackupStatus.Completed);

            repository.GetStatusHistory(logJob)
                .Should().ContainInOrder(BackupStatus.Running, BackupStatus.Completed);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Executor_ShouldFailJob_WhenRequiredLsnMetadataIsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"deadpool-worker-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var repository = new ClaimOnlyInMemoryBackupJobRepository();
            var backupExecutor = new MissingMetadataBackupExecutor();
            var metadataService = new StubMetadataService();
            var filePathService = new BackupFilePathService(tempRoot);

            var worker = new BackupExecutionWorker(
                NullLogger<BackupExecutionWorker>.Instance,
                repository,
                backupExecutor,
                filePathService,
                metadataService);

            var fullJob = new BackupJob("TestDB", BackupType.Full, Path.Combine(tempRoot, "full-placeholder.bak"));
            await repository.CreateAsync(fullJob);

            var cts = new CancellationTokenSource();
            var workerTask = worker.StartAsync(cts.Token);
            await Task.Delay(800);
            await cts.CancelAsync();
            await workerTask;

            fullJob.Status.Should().Be(BackupStatus.Failed);
            fullJob.ErrorMessage.Should().Contain("DatabaseBackupLSN");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class ClaimOnlyInMemoryBackupJobRepository : IBackupJobRepository
    {
        private readonly List<BackupJob> _jobs = new();
        private readonly Dictionary<BackupJob, List<BackupStatus>> _statusHistory = new();

        public Task CreateAsync(BackupJob backupJob)
        {
            _jobs.Add(backupJob);
            _statusHistory[backupJob] = new List<BackupStatus> { backupJob.Status };
            return Task.CompletedTask;
        }

        public Task UpdateAsync(BackupJob backupJob)
        {
            if (!_statusHistory.TryGetValue(backupJob, out var history))
            {
                history = new List<BackupStatus>();
                _statusHistory[backupJob] = history;
            }

            history.Add(backupJob.Status);
            return Task.CompletedTask;
        }

        public Task<BackupJob?> GetByIdAsync(int id) => Task.FromResult<BackupJob?>(null);

        public Task<IEnumerable<BackupJob>> GetRecentJobsAsync(string databaseName, int count)
        {
            var result = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .OrderByDescending(j => j.StartTime)
                .Take(count);
            return Task.FromResult<IEnumerable<BackupJob>>(result.ToList());
        }

        public Task<BackupJob?> GetLastSuccessfulFullBackupAsync(string databaseName)
        {
            var job = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.BackupType == BackupType.Full)
                .Where(j => j.Status == BackupStatus.Completed)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefault();

            return Task.FromResult(job);
        }

        public async Task<bool> HasSuccessfulFullBackupAsync(string databaseName)
        {
            return await GetLastSuccessfulFullBackupAsync(databaseName) != null;
        }

        public Task<IEnumerable<BackupJob>> GetPendingJobsAsync(int maxCount)
        {
            var pending = _jobs
                .Where(j => j.Status == BackupStatus.Pending)
                .OrderBy(j => j.StartTime)
                .Take(maxCount)
                .ToList();

            return Task.FromResult<IEnumerable<BackupJob>>(pending);
        }

        public Task<bool> TryClaimJobAsync(BackupJob job)
        {
            // Simulates DB-level claim that does not mutate the in-memory entity state.
            return Task.FromResult(_jobs.Contains(job) && job.Status == BackupStatus.Pending);
        }

        public Task<BackupJob?> GetLastSuccessfulBackupAsync(string databaseName, BackupType backupType)
        {
            var job = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.BackupType == backupType)
                .Where(j => j.Status == BackupStatus.Completed)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefault();

            return Task.FromResult(job);
        }

        public Task<BackupJob?> GetLastFailedBackupAsync(string databaseName)
        {
            var job = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.Status == BackupStatus.Failed)
                .OrderByDescending(j => j.StartTime)
                .FirstOrDefault();

            return Task.FromResult(job);
        }

        public Task<IEnumerable<BackupJob>> GetBackupChainAsync(string databaseName, DateTime since)
        {
            var chain = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .Where(j => j.Status == BackupStatus.Completed)
                .Where(j => j.StartTime >= since)
                .OrderBy(j => j.StartTime)
                .ToList();

            return Task.FromResult<IEnumerable<BackupJob>>(chain);
        }

        public Task<IEnumerable<BackupJob>> GetBackupsByDatabaseAsync(string databaseName)
        {
            var backups = _jobs
                .Where(j => j.DatabaseName == databaseName)
                .OrderByDescending(j => j.StartTime)
                .ToList();

            return Task.FromResult<IEnumerable<BackupJob>>(backups);
        }

        public IReadOnlyList<BackupStatus> GetStatusHistory(BackupJob job)
        {
            return _statusHistory.TryGetValue(job, out var history)
                ? history
                : new List<BackupStatus>();
        }
    }

    private sealed class StubFileBackupExecutor : IBackupExecutor
    {
        public Task ExecuteFullBackupAsync(string databaseName, string backupFilePath)
            => WriteFileAsync(backupFilePath);

        public Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath)
            => WriteFileAsync(backupFilePath);

        public Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath)
            => WriteFileAsync(backupFilePath);

        public Task<bool> VerifyBackupFileAsync(string backupFilePath)
            => Task.FromResult(File.Exists(backupFilePath));

        public Task<BackupLSNMetadata?> GetBackupLSNMetadataAsync(string databaseName, string backupFilePath)
        {
            if (backupFilePath.Contains("_FULL_", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                    firstLSN: null,
                    lastLSN: null,
                    databaseBackupLSN: 1000m,
                    checkpointLSN: 1000m));
            }

            if (backupFilePath.Contains("_DIFF_", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                    firstLSN: null,
                    lastLSN: null,
                    databaseBackupLSN: 1000m,
                    checkpointLSN: null));
            }

            if (backupFilePath.Contains("_LOG_", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                    firstLSN: 2000m,
                    lastLSN: 3000m,
                    databaseBackupLSN: 1000m,
                    checkpointLSN: null));
            }

            return Task.FromResult<BackupLSNMetadata?>(null);
        }

        public static Task WriteFileForTestAsync(string backupFilePath)
        {
            var directory = Path.GetDirectoryName(backupFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(backupFilePath, "BACKUP");
            return Task.CompletedTask;
        }

        private static Task WriteFileAsync(string backupFilePath)
            => WriteFileForTestAsync(backupFilePath);
    }

    private sealed class StubMetadataService : IDatabaseMetadataService
    {
        public Task<RecoveryModel> GetRecoveryModelAsync(string databaseName)
            => Task.FromResult(RecoveryModel.Full);
    }

    private sealed class MissingMetadataBackupExecutor : IBackupExecutor
    {
        public Task ExecuteFullBackupAsync(string databaseName, string backupFilePath)
            => StubFileBackupExecutor.WriteFileForTestAsync(backupFilePath);

        public Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath)
            => StubFileBackupExecutor.WriteFileForTestAsync(backupFilePath);

        public Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath)
            => StubFileBackupExecutor.WriteFileForTestAsync(backupFilePath);

        public Task<bool> VerifyBackupFileAsync(string backupFilePath)
            => Task.FromResult(File.Exists(backupFilePath));

        public Task<BackupLSNMetadata?> GetBackupLSNMetadataAsync(string databaseName, string backupFilePath)
            => Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                firstLSN: null,
                lastLSN: null,
                databaseBackupLSN: null,
                checkpointLSN: null));
    }

    private sealed class ConcurrencyTrackingBackupExecutor : IBackupExecutor
    {
        private readonly int _delayMs;
        private readonly ConcurrentDictionary<string, int> _activeByDatabase = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _maxByDatabase = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrencyTrackingBackupExecutor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public int GetMaxConcurrency(string databaseName)
        {
            return _maxByDatabase.TryGetValue(databaseName, out var max)
                ? max
                : 0;
        }

        public Task ExecuteFullBackupAsync(string databaseName, string backupFilePath)
            => ExecuteWithTrackingAsync(databaseName, backupFilePath);

        public Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath)
            => ExecuteWithTrackingAsync(databaseName, backupFilePath);

        public Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath)
            => ExecuteWithTrackingAsync(databaseName, backupFilePath);

        public Task<bool> VerifyBackupFileAsync(string backupFilePath)
            => Task.FromResult(File.Exists(backupFilePath));

        public Task<BackupLSNMetadata?> GetBackupLSNMetadataAsync(string databaseName, string backupFilePath)
        {
            if (backupFilePath.Contains("_FULL_", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                    firstLSN: null,
                    lastLSN: null,
                    databaseBackupLSN: 1000m,
                    checkpointLSN: 1000m));
            }

            return Task.FromResult<BackupLSNMetadata?>(new BackupLSNMetadata(
                firstLSN: 2000m,
                lastLSN: 3000m,
                databaseBackupLSN: 1000m,
                checkpointLSN: null));
        }

        private async Task ExecuteWithTrackingAsync(string databaseName, string backupFilePath)
        {
            var current = _activeByDatabase.AddOrUpdate(databaseName, 1, (_, active) => active + 1);
            _maxByDatabase.AddOrUpdate(databaseName, current, (_, max) => Math.Max(max, current));

            try
            {
                await Task.Delay(_delayMs);
                await StubFileBackupExecutor.WriteFileForTestAsync(backupFilePath);
            }
            finally
            {
                _activeByDatabase.AddOrUpdate(databaseName, 0, (_, active) => Math.Max(0, active - 1));
            }
        }
    }

    private sealed class ConcurrentClaimInMemoryBackupJobRepository : IBackupJobRepository
    {
        private readonly object _sync = new();
        private readonly List<BackupJob> _jobs = new();
        private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);

        public Task CreateAsync(BackupJob backupJob)
        {
            lock (_sync)
            {
                _jobs.Add(backupJob);
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(BackupJob backupJob) => Task.CompletedTask;

        public Task<BackupJob?> GetByIdAsync(int id) => Task.FromResult<BackupJob?>(null);

        public Task<IEnumerable<BackupJob>> GetRecentJobsAsync(string databaseName, int count)
        {
            lock (_sync)
            {
                var result = _jobs
                    .Where(j => j.DatabaseName == databaseName)
                    .OrderByDescending(j => j.StartTime)
                    .Take(count)
                    .ToList();

                return Task.FromResult<IEnumerable<BackupJob>>(result);
            }
        }

        public Task<BackupJob?> GetLastSuccessfulFullBackupAsync(string databaseName)
        {
            lock (_sync)
            {
                var job = _jobs
                    .Where(j => j.DatabaseName == databaseName)
                    .Where(j => j.BackupType == BackupType.Full)
                    .Where(j => j.Status == BackupStatus.Completed)
                    .OrderByDescending(j => j.StartTime)
                    .FirstOrDefault();

                return Task.FromResult(job);
            }
        }

        public async Task<bool> HasSuccessfulFullBackupAsync(string databaseName)
        {
            return await GetLastSuccessfulFullBackupAsync(databaseName) != null;
        }

        public Task<IEnumerable<BackupJob>> GetPendingJobsAsync(int maxCount)
        {
            lock (_sync)
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
            lock (_sync)
            {
                var key = $"{job.DatabaseName}:{job.BackupType}:{job.StartTime:O}";
                if (_claimed.Contains(key))
                    return Task.FromResult(false);

                if (!_jobs.Contains(job) || job.Status != BackupStatus.Pending)
                    return Task.FromResult(false);

                _claimed.Add(key);
                return Task.FromResult(true);
            }
        }

        public Task<BackupJob?> GetLastSuccessfulBackupAsync(string databaseName, BackupType backupType)
        {
            lock (_sync)
            {
                var job = _jobs
                    .Where(j => j.DatabaseName == databaseName)
                    .Where(j => j.BackupType == backupType)
                    .Where(j => j.Status == BackupStatus.Completed)
                    .OrderByDescending(j => j.StartTime)
                    .FirstOrDefault();

                return Task.FromResult(job);
            }
        }

        public Task<BackupJob?> GetLastFailedBackupAsync(string databaseName)
        {
            lock (_sync)
            {
                var job = _jobs
                    .Where(j => j.DatabaseName == databaseName)
                    .Where(j => j.Status == BackupStatus.Failed)
                    .OrderByDescending(j => j.StartTime)
                    .FirstOrDefault();

                return Task.FromResult(job);
            }
        }

        public Task<IEnumerable<BackupJob>> GetBackupChainAsync(string databaseName, DateTime since)
        {
            lock (_sync)
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

        public Task<IEnumerable<BackupJob>> GetBackupsByDatabaseAsync(string databaseName)
        {
            lock (_sync)
            {
                var backups = _jobs
                    .Where(j => j.DatabaseName == databaseName)
                    .OrderByDescending(j => j.StartTime)
                    .ToList();

                return Task.FromResult<IEnumerable<BackupJob>>(backups);
            }
        }
    }
}
