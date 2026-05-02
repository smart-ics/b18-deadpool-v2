using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadpool.Tests.Unit;

public class BackupExecutionWorkerTests
{
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
            => Task.FromResult<BackupLSNMetadata?>(null);

        private static Task WriteFileAsync(string backupFilePath)
        {
            var directory = Path.GetDirectoryName(backupFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(backupFilePath, "BACKUP");
            return Task.CompletedTask;
        }
    }

    private sealed class StubMetadataService : IDatabaseMetadataService
    {
        public Task<RecoveryModel> GetRecoveryModelAsync(string databaseName)
            => Task.FromResult(RecoveryModel.Full);
    }
}
