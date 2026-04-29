using Deadpool.Agent.Configuration;
using Deadpool.Agent.Workers;
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

// Core services
builder.Services.AddSingleton<IBackupJobRepository, InMemoryBackupJobRepository>();
builder.Services.AddSingleton<IScheduleTracker, InMemoryScheduleTracker>();

// Backup execution dependencies (using stubs for now)
builder.Services.AddSingleton<IBackupExecutor, StubBackupExecutor>();
builder.Services.AddSingleton<IDatabaseMetadataService, StubDatabaseMetadataService>();

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

var host = builder.Build();
host.Run();
