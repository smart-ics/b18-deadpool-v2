using Deadpool.Agent.Configuration;
using Deadpool.Core.Configuration;
using Deadpool.Agent.Infrastructure;
using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Deadpool.Infrastructure.FileCopy;
using Deadpool.Infrastructure.Persistence;
using Deadpool.Infrastructure.Scheduling;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var baseDir = "c:\\ics\\deadpool";

var current = new DirectoryInfo(baseDir);

// Walk upward to locate portable root that contains appsettings.shared.json.
while (current != null && !File.Exists(Path.Combine(current.FullName, "appsettings.shared.json")))
{
    current = current.Parent;
}

if (current == null)
{
    throw new Exception("Cannot locate appsettings.shared.json");
}

var rootDir = current.FullName;

var configuration = new ConfigurationBuilder()
    .SetBasePath(rootDir)
    .AddJsonFile("appsettings.shared.json", optional: false, reloadOnChange: true)
    .AddJsonFile("secrets.json", optional: true, reloadOnChange: true)
    .Build();

builder.Configuration.AddConfiguration(configuration);

// Backup-policy configuration
builder.Services.Configure<List<DatabaseBackupPolicyOptions>>(
    builder.Configuration.GetSection("BackupPolicies"));

var startupPolicies = builder.Configuration
    .GetSection("BackupPolicies")
    .Get<List<DatabaseBackupPolicyOptions>>()
    ?? new List<DatabaseBackupPolicyOptions>();

var productionConnectionString = builder.Configuration.GetConnectionString("ProductionDatabase");

builder.Services.Configure<RestoreOrchestratorOptions>(options =>
{
    options.DatabaseName = startupPolicies.FirstOrDefault()?.DatabaseName ?? string.Empty;
    options.AllowOverwrite = builder.Configuration.GetValue<bool>("Restore:StartupCommand:AllowOverwrite");
});

builder.Services.Configure<RestoreExecutionOptions>(options =>
{
    options.ConnectionString = productionConnectionString ?? string.Empty;
    options.CommandTimeoutSeconds = builder.Configuration.GetValue<int?>("Restore:Execution:CommandTimeoutSeconds") ?? 300;
});

// Execution worker configuration
builder.Services.Configure<ExecutionWorkerOptions>(
    builder.Configuration.GetSection("ExecutionWorker"));

// Backup copy configuration
builder.Services.Configure<BackupCopyOptions>(
    builder.Configuration.GetSection("BackupCopy"));

// Shared backup storage configuration
builder.Services.Configure<BackupStorageOptions>(
    builder.Configuration.GetSection("BackupStorage"));

// Health monitoring configuration
builder.Services.Configure<HealthMonitoringOptions>(
    builder.Configuration.GetSection("HealthMonitoring"));

// Storage monitoring configuration
builder.Services.Configure<StorageMonitoringOptions>(
    builder.Configuration.GetSection("StorageMonitoring"));

// Database pulse configuration
builder.Services.Configure<DatabasePulseOptions>(
    builder.Configuration.GetSection("DatabasePulse"));

// SQLite shared repository (used by Agent and UI)
var sqlitePath = builder.Configuration.GetValue<string>("DeadpoolDb:Path");
if (string.IsNullOrWhiteSpace(sqlitePath))
{
    throw new InvalidOperationException("DeadpoolDb:Path is required for shared SQLite database.");
}

builder.Services.AddSingleton<IBackupJobRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SqliteBackupJobRepository>>();
    return new SqliteBackupJobRepository(sqlitePath, logger);
});

// Health check repositories — SQLite, shared with UI
builder.Services.AddSingleton<IBackupHealthCheckRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SqliteBackupHealthCheckRepository>>();
    return new SqliteBackupHealthCheckRepository(sqlitePath, logger);
});
builder.Services.AddSingleton<IStorageHealthCheckRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SqliteStorageHealthCheckRepository>>();
    return new SqliteStorageHealthCheckRepository(sqlitePath, logger);
});
builder.Services.AddSingleton<IDatabasePulseRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SqliteDatabasePulseRepository>>();
    return new SqliteDatabasePulseRepository(sqlitePath, logger);
});
builder.Services.AddSingleton<IAgentHeartbeatRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SqliteAgentHeartbeatRepository>>();
    return new SqliteAgentHeartbeatRepository(sqlitePath, logger);
});
builder.Services.AddSingleton<IScheduleTracker, InMemoryScheduleTracker>();

// Storage monitoring dependencies
builder.Services.AddSingleton<IStorageInfoProvider, Deadpool.Infrastructure.Storage.FileSystemStorageInfoProvider>();
builder.Services.AddSingleton<IBackupSizeEstimator, Deadpool.Infrastructure.Estimation.RecentBackupSizeEstimator>();

// Database connectivity probe for pulse worker
builder.Services.AddSingleton<IDatabaseConnectivityProbe>(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ProductionSqlRuntime");
    var firstDb = builder.Configuration.GetSection("BackupPolicies")
        .Get<List<DatabaseBackupPolicyOptions>>()?.FirstOrDefault()?.DatabaseName;
    return ProductionSqlRuntimeFactory.CreateDatabaseConnectivityProbe(productionConnectionString, firstDb, logger);
});

// Backup execution dependencies (real SQL Server if configured, stub fallback otherwise)
builder.Services.AddSingleton<IBackupExecutor>(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ProductionSqlRuntime");
    return ProductionSqlRuntimeFactory.CreateBackupExecutor(productionConnectionString, logger);
});
builder.Services.AddSingleton<IDatabaseMetadataService>(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ProductionSqlRuntime");
    return ProductionSqlRuntimeFactory.CreateDatabaseMetadataService(productionConnectionString, logger);
});

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
var copyOptions = builder.Configuration.GetSection("BackupCopy").Get<BackupCopyOptions>()
    ?? new BackupCopyOptions();
var backupStorageOptions = builder.Configuration.GetSection("BackupStorage").Get<BackupStorageOptions>()
    ?? new BackupStorageOptions();
var configuredRemoteStoragePath = !string.IsNullOrWhiteSpace(copyOptions.RemoteStoragePath)
    ? copyOptions.RemoteStoragePath
    : backupStorageOptions.StorageFolder;
var isCopyEnabled = copyOptions.Enabled ?? !string.IsNullOrWhiteSpace(configuredRemoteStoragePath);

if (isCopyEnabled && !string.IsNullOrWhiteSpace(configuredRemoteStoragePath))
{
    builder.Services.AddSingleton<IBackupFileCopyService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<BackupFileCopyService>>();
        return new BackupFileCopyService(
            logger,
            configuredRemoteStoragePath,
            copyOptions.MaxRetryAttempts,
            copyOptions.RetryDelay);
    });

    builder.Services.AddHostedService<BackupCopyWorker>();
}

// BackupFilePathService
builder.Services.AddSingleton<BackupFilePathService>(sp =>
    new BackupFilePathService(backupStorageOptions.StagingFolder));

// BackupService (still used by existing code, but execution worker uses IBackupExecutor directly)
builder.Services.AddSingleton<BackupService>();

// Bootstrap state tracker and initialization service
builder.Services.AddSingleton<IBootstrapStateTracker, InMemoryBootstrapStateTracker>();
builder.Services.AddSingleton<IBackupChainInitializationService, BackupChainInitializationService>();
builder.Services.AddScoped<IRestorePlannerService, RestorePlannerService>();
builder.Services.AddScoped<IRestorePlanValidatorService, RestorePlanValidatorService>();
builder.Services.AddScoped<IRestoreScriptBuilderService, RestoreScriptBuilderService>();
builder.Services.AddScoped<IRestoreSafetyGuard, RestoreSafetyGuardService>();
builder.Services.AddScoped<IRestoreExecutionService, RestoreExecutionService>();
builder.Services.AddScoped<IRestoreOrchestratorService, RestoreOrchestratorService>();

// Hosted workers.
// Safety note: all BackgroundService.ExecuteAsync methods start concurrently —
// registration order does NOT guarantee sequential startup.
// Race safety for bootstrap is provided by:
//   - InMemoryBootstrapStateTracker defaults to BootstrapPending for all databases
//   - BackupSchedulerWorker.TryScheduleAsync blocks Diff/Log until status = Initialized
//   - BootstrapWorker seeds IScheduleTracker for Full after success, preventing a
//     duplicate cron-driven Full job on the scheduler's first tick.
builder.Services.AddHostedService<BootstrapWorker>();
builder.Services.AddHostedService<BackupSchedulerWorker>();
builder.Services.AddHostedService<BackupExecutionWorker>();
builder.Services.AddHostedService<BackupHealthMonitoringWorker>();
builder.Services.AddHostedService<StorageMonitoringWorker>();
builder.Services.AddHostedService<DatabasePulseWorker>();
builder.Services.AddHostedService<AgentHeartbeatWorker>();

var host = builder.Build();
var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ProductionSqlStartup");
ProductionSqlRuntimeFactory.LogConnectivityCheckAsync(productionConnectionString, startupLogger)
    .GetAwaiter()
    .GetResult();

// Temporary runtime restore command path.
// When enabled via configuration, this invokes the real orchestration flow:
// Planner -> Validator -> Execution guard.
var runRestoreOnStartup = builder.Configuration.GetValue<bool>("Restore:StartupCommand:Enabled");
if (runRestoreOnStartup)
{
    var targetTimeText = builder.Configuration["Restore:StartupCommand:TargetTimeUtc"];
    if (!DateTime.TryParse(targetTimeText, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var targetTime))
    {
        throw new InvalidOperationException(
            "Restore:StartupCommand:TargetTimeUtc must be a valid UTC DateTime when Restore:StartupCommand:Enabled is true.");
    }

    using var scope = host.Services.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<IRestoreOrchestratorService>();
    orchestrator.ExecuteRestore(targetTime).GetAwaiter().GetResult();
}

host.Run();
