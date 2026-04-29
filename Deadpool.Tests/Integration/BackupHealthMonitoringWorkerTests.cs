using Deadpool.Agent.Configuration;
using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Services;
using Deadpool.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Deadpool.Tests.Integration;

public class BackupHealthMonitoringWorkerTests
{
    [Fact]
    public async Task HealthMonitoringWorker_ShouldDetectHealthyState()
    {
        var jobRepo = new InMemoryBackupJobRepository();
        var healthCheckRepo = new InMemoryBackupHealthCheckRepository();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var diffBackup = CreateCompletedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-2));
        var logBackup = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddMinutes(-10));

        await jobRepo.CreateAsync(fullBackup);
        await jobRepo.CreateAsync(diffBackup);
        await jobRepo.CreateAsync(logBackup);

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new()
            {
                DatabaseName = "TestDB",
                RecoveryModel = "Full",
                FullBackupCron = "0 0 * * *",
                DifferentialBackupCron = "0 */6 * * *",
                TransactionLogBackupCron = "*/15 * * * *",
                RetentionDays = 7
            }
        };

        var healthOptions = new HealthMonitoringOptions
        {
            CheckInterval = TimeSpan.FromMinutes(5),
            FullBackupOverdueThreshold = TimeSpan.FromHours(24),
            DifferentialBackupOverdueThreshold = TimeSpan.FromHours(6),
            LogBackupOverdueThreshold = TimeSpan.FromMinutes(30),
            ChainLookbackPeriod = TimeSpan.FromDays(7)
        };

        var backupHealthOptions = new BackupHealthOptions(
            healthOptions.FullBackupOverdueThreshold,
            healthOptions.DifferentialBackupOverdueThreshold,
            healthOptions.LogBackupOverdueThreshold,
            healthOptions.ChainLookbackPeriod
        );

        var healthService = new BackupHealthMonitoringService(jobRepo, backupHealthOptions);

        var worker = new BackupHealthMonitoringWorker(
            NullLogger<BackupHealthMonitoringWorker>.Instance,
            healthService,
            healthCheckRepo,
            Options.Create(policies),
            Options.Create(healthOptions)
        );

        var cts = new CancellationTokenSource();
        var workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        var latestCheck = await healthCheckRepo.GetLatestHealthCheckAsync("TestDB");
        latestCheck.Should().NotBeNull();
        latestCheck!.IsHealthy().Should().BeTrue();
    }

    [Fact]
    public async Task HealthMonitoringWorker_ShouldDetectCriticalState_WhenNoFullBackup()
    {
        var jobRepo = new InMemoryBackupJobRepository();
        var healthCheckRepo = new InMemoryBackupHealthCheckRepository();

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new()
            {
                DatabaseName = "TestDB",
                RecoveryModel = "Full",
                FullBackupCron = "0 0 * * *",
                DifferentialBackupCron = "0 */6 * * *",
                TransactionLogBackupCron = "*/15 * * * *",
                RetentionDays = 7
            }
        };

        var healthOptions = new HealthMonitoringOptions
        {
            CheckInterval = TimeSpan.FromMinutes(5),
            FullBackupOverdueThreshold = TimeSpan.FromHours(24),
            DifferentialBackupOverdueThreshold = TimeSpan.FromHours(6),
            LogBackupOverdueThreshold = TimeSpan.FromMinutes(30),
            ChainLookbackPeriod = TimeSpan.FromDays(7)
        };

        var backupHealthOptions = new BackupHealthOptions(
            healthOptions.FullBackupOverdueThreshold,
            healthOptions.DifferentialBackupOverdueThreshold,
            healthOptions.LogBackupOverdueThreshold,
            healthOptions.ChainLookbackPeriod
        );

        var healthService = new BackupHealthMonitoringService(jobRepo, backupHealthOptions);

        var worker = new BackupHealthMonitoringWorker(
            NullLogger<BackupHealthMonitoringWorker>.Instance,
            healthService,
            healthCheckRepo,
            Options.Create(policies),
            Options.Create(healthOptions)
        );

        var cts = new CancellationTokenSource();
        var workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        var latestCheck = await healthCheckRepo.GetLatestHealthCheckAsync("TestDB");
        latestCheck.Should().NotBeNull();
        latestCheck!.IsCritical().Should().BeTrue();
        latestCheck.CriticalFindings.Should().Contain(f => f.Contains("No successful full backup found"));
    }

    [Fact]
    public async Task HealthMonitoringWorker_ShouldDetectWarning_WhenBackupOverdue()
    {
        var jobRepo = new InMemoryBackupJobRepository();
        var healthCheckRepo = new InMemoryBackupHealthCheckRepository();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-30));
        await jobRepo.CreateAsync(fullBackup);

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new()
            {
                DatabaseName = "TestDB",
                RecoveryModel = "Simple",
                FullBackupCron = "0 0 * * *",
                DifferentialBackupCron = "0 */6 * * *",
                TransactionLogBackupCron = "",
                RetentionDays = 7
            }
        };

        var healthOptions = new HealthMonitoringOptions
        {
            CheckInterval = TimeSpan.FromMinutes(5),
            FullBackupOverdueThreshold = TimeSpan.FromHours(24),
            DifferentialBackupOverdueThreshold = TimeSpan.FromHours(6),
            LogBackupOverdueThreshold = TimeSpan.FromMinutes(30),
            ChainLookbackPeriod = TimeSpan.FromDays(7)
        };

        var backupHealthOptions = new BackupHealthOptions(
            healthOptions.FullBackupOverdueThreshold,
            healthOptions.DifferentialBackupOverdueThreshold,
            healthOptions.LogBackupOverdueThreshold,
            healthOptions.ChainLookbackPeriod
        );

        var healthService = new BackupHealthMonitoringService(jobRepo, backupHealthOptions);

        var worker = new BackupHealthMonitoringWorker(
            NullLogger<BackupHealthMonitoringWorker>.Instance,
            healthService,
            healthCheckRepo,
            Options.Create(policies),
            Options.Create(healthOptions)
        );

        var cts = new CancellationTokenSource();
        var workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        var latestCheck = await healthCheckRepo.GetLatestHealthCheckAsync("TestDB");
        latestCheck.Should().NotBeNull();
        latestCheck!.HasWarnings().Should().BeTrue();
        latestCheck.Warnings.Should().Contain(w => w.Contains("Full backup overdue"));
    }

    [Fact]
    public async Task HealthMonitoringWorker_ShouldCheckMultipleDatabases()
    {
        var jobRepo = new InMemoryBackupJobRepository();
        var healthCheckRepo = new InMemoryBackupHealthCheckRepository();

        var fullBackup1 = CreateCompletedJob("DB1", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var fullBackup2 = CreateCompletedJob("DB2", BackupType.Full, DateTime.UtcNow.AddHours(-12));

        await jobRepo.CreateAsync(fullBackup1);
        await jobRepo.CreateAsync(fullBackup2);

        var policies = new List<DatabaseBackupPolicyOptions>
        {
            new()
            {
                DatabaseName = "DB1",
                RecoveryModel = "Simple",
                FullBackupCron = "0 0 * * *",
                DifferentialBackupCron = "0 */6 * * *",
                TransactionLogBackupCron = "",
                RetentionDays = 7
            },
            new()
            {
                DatabaseName = "DB2",
                RecoveryModel = "Simple",
                FullBackupCron = "0 0 * * *",
                DifferentialBackupCron = "0 */6 * * *",
                TransactionLogBackupCron = "",
                RetentionDays = 7
            }
        };

        var healthOptions = new HealthMonitoringOptions
        {
            CheckInterval = TimeSpan.FromMinutes(5),
            FullBackupOverdueThreshold = TimeSpan.FromHours(24),
            DifferentialBackupOverdueThreshold = TimeSpan.FromHours(6),
            LogBackupOverdueThreshold = TimeSpan.FromMinutes(30),
            ChainLookbackPeriod = TimeSpan.FromDays(7)
        };

        var backupHealthOptions = new BackupHealthOptions(
            healthOptions.FullBackupOverdueThreshold,
            healthOptions.DifferentialBackupOverdueThreshold,
            healthOptions.LogBackupOverdueThreshold,
            healthOptions.ChainLookbackPeriod
        );

        var healthService = new BackupHealthMonitoringService(jobRepo, backupHealthOptions);

        var worker = new BackupHealthMonitoringWorker(
            NullLogger<BackupHealthMonitoringWorker>.Instance,
            healthService,
            healthCheckRepo,
            Options.Create(policies),
            Options.Create(healthOptions)
        );

        var cts = new CancellationTokenSource();
        var workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        var check1 = await healthCheckRepo.GetLatestHealthCheckAsync("DB1");
        var check2 = await healthCheckRepo.GetLatestHealthCheckAsync("DB2");

        check1.Should().NotBeNull();
        check2.Should().NotBeNull();
        check1!.IsHealthy().Should().BeTrue();
        check2!.IsHealthy().Should().BeTrue();
    }

    private static BackupJob CreateCompletedJob(string databaseName, BackupType backupType, DateTime startTime)
    {
        // Use non-rooted path so file existence check will skip it
        var job = new BackupJob(databaseName, backupType, $"backup/{databaseName}_{backupType}.bak");

        typeof(BackupJob)
            .GetField("<StartTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, startTime);

        job.MarkAsRunning();
        job.MarkAsCompleted(1024);

        // Set EndTime to match startTime so age calculation works correctly
        typeof(BackupJob)
            .GetField("<EndTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, startTime.AddMinutes(5));

        return job;
    }
}
