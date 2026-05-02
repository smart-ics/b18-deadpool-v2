using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Deadpool.Core.Configuration;
using Deadpool.Infrastructure.Persistence;
using Deadpool.UI.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deadpool.UI;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

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

        // Build service collection (pragmatic WinForms DI setup)
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Load dashboard settings
        var dashboardOptions = configuration.GetSection("Dashboard").Get<DashboardOptions>() ?? new DashboardOptions();
        var backupStorageOptions = configuration.GetSection("BackupStorage").Get<BackupStorageOptions>()
            ?? new BackupStorageOptions();
        var policyOptions = configuration.GetSection("BackupPolicies").Get<List<DatabaseBackupPolicyOptions>>()
            ?? new List<DatabaseBackupPolicyOptions>();
        var selectedPolicy = policyOptions.FirstOrDefault();
        if (selectedPolicy == null)
        {
            throw new InvalidOperationException("BackupPolicies must contain at least one policy for the dashboard.");
        }

        // Launch dashboard
        var dashboardService = serviceProvider.GetRequiredService<IDashboardMonitoringService>();
        var policyFormatter = serviceProvider.GetRequiredService<IBackupPolicyDisplayFormatter>();
        var dashboard = new MonitoringDashboard(
            dashboardService,
            policyFormatter,
            serviceProvider.GetRequiredService<ILogger<MonitoringDashboard>>(),
            selectedPolicy.DatabaseName,
            backupStorageOptions.StorageFolder,
            dashboardOptions.AutoRefreshIntervalSeconds,
            selectedPolicy);

        // Store service provider for child forms
        dashboard.Tag = serviceProvider;

        Application.Run(dashboard);
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration options
        services.Configure<DashboardOptions>(configuration.GetSection("Dashboard"));
        services.Configure<List<DatabaseBackupPolicyOptions>>(configuration.GetSection("BackupPolicies"));

        var backupPolicies = configuration.GetSection("BackupPolicies").Get<List<DatabaseBackupPolicyOptions>>()
            ?? new List<DatabaseBackupPolicyOptions>();
        services.Configure<RestoreOrchestratorOptions>(options =>
        {
            options.DatabaseName = backupPolicies.FirstOrDefault()?.DatabaseName ?? string.Empty;
        });

        // Logging (simple debug logging for UI)
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // SQLite shared repositories (read-only from UI — Agent writes)
        var sqlitePath = configuration.GetValue<string>("DeadpoolDb:Path");
        if (string.IsNullOrWhiteSpace(sqlitePath))
        {
            throw new InvalidOperationException("DeadpoolDb:Path is required for shared SQLite database.");
        }

        services.AddSingleton<IBackupJobRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteBackupJobRepository>>();
            return new SqliteBackupJobRepository(sqlitePath, logger);
        });

        services.AddSingleton<IBackupHealthCheckRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteBackupHealthCheckRepository>>();
            return new SqliteBackupHealthCheckRepository(sqlitePath, logger);
        });

        services.AddSingleton<IStorageHealthCheckRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteStorageHealthCheckRepository>>();
            return new SqliteStorageHealthCheckRepository(sqlitePath, logger);
        });

        services.AddSingleton<IDatabasePulseRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteDatabasePulseRepository>>();
            return new SqliteDatabasePulseRepository(sqlitePath, logger);
        });

        services.AddSingleton<IAgentHeartbeatRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteAgentHeartbeatRepository>>();
            return new SqliteAgentHeartbeatRepository(sqlitePath, logger);
        });

        // Core dashboard services
        services.AddSingleton<IDashboardMonitoringService, DashboardMonitoringService>();
        services.AddSingleton<IBackupJobMonitoringService, BackupJobMonitoringService>();
        services.AddSingleton<ICronScheduleDescriptionService, CronScheduleDescriptionService>();
        services.AddSingleton<IBackupPolicyDisplayFormatter, BackupPolicyDisplayFormatter>();
        services.AddScoped<IRestorePlannerService, RestorePlannerService>();
        services.AddScoped<IRestorePlanValidatorService, RestorePlanValidatorService>();
        services.AddScoped<IRestoreExecutionService, RestoreExecutionService>();
        services.AddScoped<IRestoreOrchestratorService, RestoreOrchestratorService>();
    }

}
