using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
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

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

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
            .AddJsonFile(Path.Combine(baseDir, "appsettings.json"), optional: true, reloadOnChange: true)
            .Build();

        // Build service collection (pragmatic WinForms DI setup)
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Load dashboard settings
        var dashboardOptions = configuration.GetSection("Dashboard").Get<DashboardOptions>() ?? new DashboardOptions();
        var policyOptions = configuration.GetSection("BackupPolicies").Get<List<DatabaseBackupPolicyOptions>>()
            ?? new List<DatabaseBackupPolicyOptions>();
        var selectedPolicy = policyOptions.FirstOrDefault(x =>
            string.Equals(x.DatabaseName, dashboardOptions.DatabaseName, StringComparison.OrdinalIgnoreCase));

        // Launch dashboard
        var dashboardService = serviceProvider.GetRequiredService<IDashboardMonitoringService>();
        var policyFormatter = serviceProvider.GetRequiredService<IBackupPolicyDisplayFormatter>();
        var dashboard = new MonitoringDashboard(
            dashboardService,
            policyFormatter,
            serviceProvider.GetRequiredService<ILogger<MonitoringDashboard>>(),
            dashboardOptions.DatabaseName,
            dashboardOptions.BackupVolumePath,
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

        // Core dashboard services
        services.AddSingleton<IDashboardMonitoringService, DashboardMonitoringService>();
        services.AddSingleton<IBackupJobMonitoringService, BackupJobMonitoringService>();
        services.AddSingleton<ICronScheduleDescriptionService, CronScheduleDescriptionService>();
        services.AddSingleton<IBackupPolicyDisplayFormatter, BackupPolicyDisplayFormatter>();
    }

}
