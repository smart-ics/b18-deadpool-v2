using Deadpool.Agent.Configuration;
using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Deadpool.Infrastructure.BackupExecution;
using Deadpool.Infrastructure.FileCopy;
using Deadpool.Infrastructure.Metadata;
using Deadpool.Infrastructure.Persistence;
using Deadpool.Infrastructure.Scheduling;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Backup-policy configuration
builder.Services.Configure<List<DatabaseBackupPolicyOptions>>(
    builder.Configuration.GetSection("BackupPolicies"));

// Execution worker configuration
builder.Services.Configure<ExecutionWorkerOptions>(
    builder.Configuration.GetSection("ExecutionWorker"));

// Backup copy configuration
builder.Services.Configure<BackupCopyOptions>(
    builder.Configuration.GetSection("BackupCopy"));

// Health monitoring configuration
builder.Services.Configure<HealthMonitoringOptions>(
    builder.Configuration.GetSection("HealthMonitoring"));

// Storage monitoring configuration
builder.Services.Configure<StorageMonitoringOptions>(
    builder.Configuration.GetSection("StorageMonitoring"));

// Core services
builder.Services.AddSingleton<IBackupJobRepository, InMemoryBackupJobRepository>();
builder.Services.AddSingleton<IBackupHealthCheckRepository, InMemoryBackupHealthCheckRepository>();
builder.Services.AddSingleton<IStorageHealthCheckRepository, InMemoryStorageHealthCheckRepository>();
builder.Services.AddSingleton<IScheduleTracker, InMemoryScheduleTracker>();

// Storage monitoring dependencies
builder.Services.AddSingleton<IStorageInfoProvider, Deadpool.Infrastructure.Storage.FileSystemStorageInfoProvider>();
builder.Services.AddSingleton<IBackupSizeEstimator, Deadpool.Infrastructure.Estimation.RecentBackupSizeEstimator>();

// Backup execution dependencies (using stubs for now)
builder.Services.AddSingleton<IBackupExecutor, StubBackupExecutor>();
builder.Services.AddSingleton<IDatabaseMetadataService, StubDatabaseMetadataService>();

// Health monitoring service
builder.Services.AddSingleton<IBackupHealthMonitoringService>(sp =>
{
    var healthOptions = sp.GetRequiredService<IOptions<HealthMonitoringOptions>>().Value;
    var repository = sp.GetRequiredService<IBackupJobRepository>();

    var backupHealthOptions = new BackupHealthOptions(
        fullBackupOverdueThreshold: healthOptions.FullBackupOverdueThreshold,
        differentialBackupOverdueThreshold: healthOptions.DifferentialBackupOverdueThreshold,
        logBackupOverdueThreshold: healthOptions.LogBackupOverdueThreshold,
        chainLookbackPeriod: healthOptions.ChainLookbackPeriod
    );

    return new BackupHealthMonitoringService(repository, backupHealthOptions);
});

// Storage monitoring service
builder.Services.AddSingleton<IStorageMonitoringService>(sp =>
{
    var storageOptions = sp.GetRequiredService<IOptions<StorageMonitoringOptions>>().Value;
    var storageInfoProvider = sp.GetRequiredService<IStorageInfoProvider>();
    var backupSizeEstimator = sp.GetRequiredService<IBackupSizeEstimator>();

    var storageHealthOptions = new StorageHealthOptions(
        warningThresholdPercentage: storageOptions.WarningThresholdPercentage,
        criticalThresholdPercentage: storageOptions.CriticalThresholdPercentage,
        minimumWarningFreeSpaceBytes: storageOptions.MinimumWarningFreeSpaceGB * 1024L * 1024 * 1024,
        minimumCriticalFreeSpaceBytes: storageOptions.MinimumCriticalFreeSpaceGB * 1024L * 1024 * 1024
    );

    return new StorageMonitoringService(storageInfoProvider, storageHealthOptions, backupSizeEstimator);
});

// Backup file copy service (conditional registration based on configuration)
builder.Services.AddSingleton<IBackupFileCopyService?>(sp =>
{
    var copyOptions = sp.GetRequiredService<IOptions<BackupCopyOptions>>().Value;

    if (!copyOptions.Enabled)
        return null; // Copy disabled

    var logger = sp.GetRequiredService<ILogger<BackupFileCopyService>>();

    return new BackupFileCopyService(
        logger,
        copyOptions.RemoteStoragePath,
        copyOptions.MaxRetryAttempts,
        copyOptions.RetryDelay);
});

// BackupFilePathService
builder.Services.AddSingleton<BackupFilePathService>(sp =>
    new BackupFilePathService("C:\\Backups")); // TODO: Move to configuration

// BackupService (still used by existing code, but execution worker uses IBackupExecutor directly)
builder.Services.AddSingleton<BackupService>();

// Hosted workers
builder.Services.AddHostedService<BackupSchedulerWorker>();
builder.Services.AddHostedService<BackupExecutionWorker>();
builder.Services.AddHostedService<BackupHealthMonitoringWorker>();
builder.Services.AddHostedService<StorageMonitoringWorker>();

var host = builder.Build();
host.Run();
