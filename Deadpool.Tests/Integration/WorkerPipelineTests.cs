using Deadpool.Agent.Configuration;
using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Deadpool.Infrastructure.BackupExecution;
using Deadpool.Infrastructure.Metadata;
using Deadpool.Infrastructure.Persistence;
using Deadpool.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deadpool.Tests.Integration;
public class WorkerPipelineTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static (BackupSchedulerWorker scheduler, BackupExecutionWorker executor, InMemoryBackupJobRepository repo)
        BuildPipeline(List<DatabaseBackupPolicyOptions> policies)
    {
        var repo = new InMemoryBackupJobRepository();
        var tracker = new InMemoryScheduleTracker();
        var filePathService = new BackupFilePathService("C:\\Backups");
        var backupExecutor = new StubBackupExecutor();
        var metadataService = new StubDatabaseMetadataService();

        var scheduler = new BackupSchedulerWorker(
            NullLogger<BackupSchedulerWorker>.Instance,
            repo,
            tracker,
            Options.Create(policies));

        var executorOptions = new ExecutionWorkerOptions
        {
            StaleJobThreshold = TimeSpan.FromHours(2)
        };

        var copyOptions = new BackupCopyOptions
        {
            RemoteStoragePath = "" // Disabled by default in tests
        };

        var executor = new BackupExecutionWorker(
            NullLogger<BackupExecutionWorker>.Instance,
            repo,
            backupExecutor,
            filePathService,
            metadataService,
            Options.Create(executorOptions),
            Options.Create(copyOptions),
            copyService: null);

        return (scheduler, executor, repo);
    }

    private static List<DatabaseBackupPolicyOptions> DefaultPolicies()
    {
        return new List<DatabaseBackupPolicyOptions>
        {
            new()
            {
                DatabaseName = "TestDB",
                FullBackupCron = "0 12 * * *",        // Daily at noon
                DifferentialBackupCron = "0 1 * * *", // Daily at 01:00
                TransactionLogBackupCron = "*/15 * * * *" // Every 15 minutes
            }
        };
    }

    // ── End-to-End Pipeline Tests ────────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_ShouldExecuteScheduledBackup_EndToEnd()
    {
        // Arrange
        var (scheduler, executor, repo) = BuildPipeline(DefaultPolicies());
        var now = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc); // Just after noon

        // Act - Scheduler creates job
        await scheduler.TickAsync(now, CancellationToken.None);

        // Verify job created
        var pendingJobs = await repo.GetPendingJobsAsync(10);
        pendingJobs.Should().ContainSingle(j => j.BackupType == BackupType.Full);

        // Act - Executor processes job
        var cts = new CancellationTokenSource();
        var executorTask = executor.StartAsync(cts.Token);
        await Task.Delay(1000); // Let executor poll and process
        await cts.CancelAsync();
        await executorTask;

        // Assert
        var completedJobs = await repo.GetRecentJobsAsync("TestDB", 10);
        completedJobs.Should().Contain(j =>
            j.BackupType == BackupType.Full &&
            j.Status == BackupStatus.Completed);
    }

    [Fact]
    public async Task Executor_ShouldClaimAndExecutePendingJob()
    {
        // Arrange
        var (_, executor, repo) = BuildPipeline(DefaultPolicies());

        // Manually create a pending job (simulating scheduler)
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\Backups\\test.bak");
        await repo.CreateAsync(job);

        job.Status.Should().Be(BackupStatus.Pending);

        // Act - Let executor process
        var cts = new CancellationTokenSource();
        var executorTask = executor.StartAsync(cts.Token);
        await Task.Delay(1000);
        await cts.CancelAsync();
        await executorTask;

        // Assert
        job.Status.Should().Be(BackupStatus.Completed);
        job.FileSizeBytes.Should().BeGreaterThan(0);
        job.EndTime.Should().NotBeNull();
    }

    [Fact]
    public async Task Executor_ShouldNotExecuteSameJobTwice_WhenMultipleWorkersRun()
    {
        // Arrange
        var repo = new InMemoryBackupJobRepository();
        var filePathService = new BackupFilePathService("C:\\Backups");
        var backupExecutor = new StubBackupExecutor();
        var metadataService = new StubDatabaseMetadataService();

        var executorOptions = new ExecutionWorkerOptions
        {
            StaleJobThreshold = TimeSpan.FromHours(2)
        };

        var copyOptions = new BackupCopyOptions { RemoteStoragePath = "" };

        // Create two executor workers
        var executor1 = new BackupExecutionWorker(
            NullLogger<BackupExecutionWorker>.Instance,
            repo,
            backupExecutor,
            filePathService,
            metadataService,
            Options.Create(executorOptions),
            Options.Create(copyOptions));

        var executor2 = new BackupExecutionWorker(
            NullLogger<BackupExecutionWorker>.Instance,
            repo,
            backupExecutor,
            filePathService,
            metadataService,
            Options.Create(executorOptions),
            Options.Create(copyOptions));

        // Create pending job
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\Backups\\test.bak");
        await repo.CreateAsync(job);

        // Act - Run both executors concurrently
        var cts = new CancellationTokenSource();
        var task1 = executor1.StartAsync(cts.Token);
        var task2 = executor2.StartAsync(cts.Token);

        await Task.Delay(1500); // Let them compete for the job

        await cts.CancelAsync();
        await Task.WhenAll(task1, task2);

        // Assert - Job should be completed exactly once
        job.Status.Should().Be(BackupStatus.Completed);

        // Only one execution (no duplicate)
        var allJobs = await repo.GetRecentJobsAsync("TestDB", 10);
        allJobs.Count(j => j.BackupType == BackupType.Full).Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleMultipleBackupTypes()
    {
        // Arrange
        var (scheduler, executor, repo) = BuildPipeline(DefaultPolicies());

        // Trigger multiple schedules
        var noon = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc);     // Full due
        var oneAM = new DateTime(2024, 1, 1, 1, 1, 0, DateTimeKind.Utc);     // Diff due
        var logTime = new DateTime(2024, 1, 1, 0, 15, 0, DateTimeKind.Utc);  // Log due

        // Act - Schedule jobs
        await scheduler.TickAsync(noon, CancellationToken.None);
        await scheduler.TickAsync(oneAM, CancellationToken.None);
        await scheduler.TickAsync(logTime, CancellationToken.None);

        // Execute jobs
        var cts = new CancellationTokenSource();
        var executorTask = executor.StartAsync(cts.Token);
        await Task.Delay(2000); // Let executor process all jobs
        await cts.CancelAsync();
        await executorTask;

        // Assert
        var jobs = await repo.GetRecentJobsAsync("TestDB", 20);
        jobs.Should().Contain(j => j.BackupType == BackupType.Full && j.Status == BackupStatus.Completed);
        jobs.Should().Contain(j => j.BackupType == BackupType.Differential && j.Status == BackupStatus.Completed);
        jobs.Should().Contain(j => j.BackupType == BackupType.TransactionLog && j.Status == BackupStatus.Completed);
    }

    [Fact]
    public async Task Executor_ShouldMarkJobAsFailed_WhenExecutionFails()
    {
        // Arrange - Use a failing executor
        var repo = new InMemoryBackupJobRepository();
        var filePathService = new BackupFilePathService("C:\\Backups");
        var failingExecutor = new FailingBackupExecutor();
        var metadataService = new StubDatabaseMetadataService();

        var executorOptions = new ExecutionWorkerOptions
        {
            StaleJobThreshold = TimeSpan.FromHours(2)
        };

        var copyOptions = new BackupCopyOptions { RemoteStoragePath = "" };

        var executor = new BackupExecutionWorker(
            NullLogger<BackupExecutionWorker>.Instance,
            repo,
            failingExecutor,
            filePathService,
            metadataService,
            Options.Create(executorOptions),
            Options.Create(copyOptions));

        // Create pending job
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\Backups\\test.bak");
        await repo.CreateAsync(job);

        // Act
        var cts = new CancellationTokenSource();
        var executorTask = executor.StartAsync(cts.Token);
        await Task.Delay(1000);
        await cts.CancelAsync();
        await executorTask;

        // Assert
        job.Status.Should().Be(BackupStatus.Failed);
        job.ErrorMessage.Should().NotBeNullOrEmpty();
        job.EndTime.Should().NotBeNull();
    }

    // ── Restart Recovery ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Scheduler_ShouldNotDuplicateJobs_AfterRestart()
    {
        // Arrange
        var policies = DefaultPolicies();
        var repo = new InMemoryBackupJobRepository();
        var tracker = new InMemoryScheduleTracker();

        // First scheduler run
        var scheduler1 = new BackupSchedulerWorker(
            NullLogger<BackupSchedulerWorker>.Instance,
            repo,
            tracker,
            Options.Create(policies));

        var now = new DateTime(2024, 1, 1, 12, 1, 0, DateTimeKind.Utc);
        await scheduler1.TickAsync(now, CancellationToken.None);

        var jobsAfterFirstRun = await repo.GetPendingJobsAsync(10);
        var fullJobCount = jobsAfterFirstRun.Count(j => j.BackupType == BackupType.Full);
        fullJobCount.Should().Be(1); // One full backup

        // Simulate restart - new scheduler instance, same repo and tracker
        var scheduler2 = new BackupSchedulerWorker(
            NullLogger<BackupSchedulerWorker>.Instance,
            repo,
            tracker,
            Options.Create(policies));

        // Act - Same time, should not create duplicate
        await scheduler2.TickAsync(now, CancellationToken.None);

        // Assert
        var jobsAfterRestart = await repo.GetPendingJobsAsync(10);
        var fullJobCountAfter = jobsAfterRestart.Count(j => j.BackupType == BackupType.Full);
        fullJobCountAfter.Should().Be(1); // Still only one full backup job
    }

    // ── Stale Job Recovery ───────────────────────────────────────────────────────

    [Fact]
    public async Task Executor_ShouldRecoverStaleRunningJob()
    {
        // Arrange
        var repo = new InMemoryBackupJobRepository();
        var filePathService = new BackupFilePathService("C:\\Backups");
        var backupExecutor = new StubBackupExecutor();
        var metadataService = new StubDatabaseMetadataService();

        // Create a job and mark it as Running (simulating executor crash)
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\Backups\\test.bak");
        await repo.CreateAsync(job);
        job.MarkAsRunning();

        // Simulate the job being stale (started > 2 hours ago)
        // We'll use a very short threshold for testing
        var executorOptions = new ExecutionWorkerOptions
        {
            StaleJobThreshold = TimeSpan.FromMilliseconds(100)
        };

        await Task.Delay(150); // Ensure job is stale

        var copyOptions = new BackupCopyOptions { RemoteStoragePath = "" };

        var executor = new BackupExecutionWorker(
            NullLogger<BackupExecutionWorker>.Instance,
            repo,
            backupExecutor,
            filePathService,
            metadataService,
            Options.Create(executorOptions),
            Options.Create(copyOptions));

        // Act - Executor should pick up and complete the stale job
        var cts = new CancellationTokenSource();
        var executorTask = executor.StartAsync(cts.Token);
        await Task.Delay(1000);
        await cts.CancelAsync();
        await executorTask;

        // Assert
        job.Status.Should().Be(BackupStatus.Completed);
        job.FileSizeBytes.Should().BeGreaterThan(0);
        job.EndTime.Should().NotBeNull();
    }

    [Fact]
    public async Task Executor_ShouldNotPickUpRecentRunningJob()
    {
        // Arrange
        var repo = new InMemoryBackupJobRepository();
        var filePathService = new BackupFilePathService("C:\\Backups");
        var backupExecutor = new StubBackupExecutor();
        var metadataService = new StubDatabaseMetadataService();

        // Create a job and mark it as Running
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\Backups\\test.bak");
        await repo.CreateAsync(job);
        job.MarkAsRunning();

        // Use long threshold - job is NOT stale yet
        var executorOptions = new ExecutionWorkerOptions
        {
            StaleJobThreshold = TimeSpan.FromHours(2)
        };

        var copyOptions = new BackupCopyOptions { RemoteStoragePath = "" };

        var executor = new BackupExecutionWorker(
            NullLogger<BackupExecutionWorker>.Instance,
            repo,
            backupExecutor,
            filePathService,
            metadataService,
            Options.Create(executorOptions),
            Options.Create(copyOptions));

        // Act - Executor should NOT pick up the recent Running job
        var cts = new CancellationTokenSource();
        var executorTask = executor.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await executorTask;

        // Assert - Job should still be Running (not completed)
        job.Status.Should().Be(BackupStatus.Running);
        job.FileSizeBytes.Should().BeNull();
        job.EndTime.Should().BeNull();
    }

    [Fact]
    public async Task Repository_GetPendingOrStaleJobsAsync_ShouldReturnBothPendingAndStale()
    {
        // Arrange
        var repo = new InMemoryBackupJobRepository();

        // Create pending job
        var pendingJob = new BackupJob("DB1", BackupType.Full, "C:\\pending.bak");
        await repo.CreateAsync(pendingJob);

        // Create stale running job
        var staleJob = new BackupJob("DB2", BackupType.Differential, "C:\\stale.bak");
        await repo.CreateAsync(staleJob);
        staleJob.MarkAsRunning();

        await Task.Delay(150); // Make staleJob actually stale

        // Create recent running job AFTER the delay
        var recentJob = new BackupJob("DB3", BackupType.TransactionLog, "C:\\recent.bak");
        await repo.CreateAsync(recentJob);
        recentJob.MarkAsRunning();

        // Act
        var jobs = await repo.GetPendingOrStaleJobsAsync(maxCount: 10, TimeSpan.FromMilliseconds(100));

        // Assert
        var jobList = jobs.ToList();
        jobList.Should().Contain(j => j.DatabaseName == "DB1"); // Pending
        jobList.Should().Contain(j => j.DatabaseName == "DB2"); // Stale Running
        jobList.Should().NotContain(j => j.DatabaseName == "DB3"); // Recent Running (not stale)
    }

    // ── Helper: Failing Executor ─────────────────────────────────────────────────

    private class FailingBackupExecutor : IBackupExecutor
    {
        public Task ExecuteFullBackupAsync(string databaseName, string backupFilePath)
            => throw new InvalidOperationException("Simulated backup failure");

        public Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath)
            => throw new InvalidOperationException("Simulated backup failure");

        public Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath)
            => throw new InvalidOperationException("Simulated backup failure");

        public Task<bool> VerifyBackupFileAsync(string backupFilePath)
            => Task.FromResult(false);
    }
}
