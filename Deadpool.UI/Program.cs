using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Deadpool.Infrastructure.Persistence;
using Deadpool.Infrastructure.Storage;
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

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Build service collection (pragmatic WinForms DI setup)
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Load dashboard settings
        var dashboardOptions = configuration.GetSection("Dashboard").Get<DashboardOptions>() ?? new DashboardOptions();

        // Launch dashboard
        var dashboardService = serviceProvider.GetRequiredService<IDashboardMonitoringService>();
        var dashboard = new MonitoringDashboard(
            dashboardService, 
            dashboardOptions.DatabaseName, 
            dashboardOptions.BackupVolumePath,
            dashboardOptions.AutoRefreshIntervalSeconds);

        Application.Run(dashboard);
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration options
        services.Configure<DashboardOptions>(configuration.GetSection("Dashboard"));
        services.Configure<DeadpoolOptions>(configuration.GetSection("Deadpool"));

        // Logging (simple debug logging for UI)
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // SQLite shared repository
        var deadpoolOptions = configuration.GetSection("Deadpool").Get<DeadpoolOptions>() ?? new DeadpoolOptions();
        var sqlitePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, deadpoolOptions.SqliteDatabasePath);

        services.AddSingleton<IBackupJobRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteBackupJobRepository>>();
            return new SqliteBackupJobRepository(sqlitePath, logger);
        });

        // Core services
        services.AddSingleton<IDashboardMonitoringService, DashboardMonitoringService>();
        services.AddSingleton<IStorageMonitoringService, StorageMonitoringService>();

        // Other repositories (still in-memory for now)
        services.AddSingleton<IStorageHealthCheckRepository, InMemoryStorageHealthCheckRepository>();

        // Storage abstraction
        services.AddSingleton<IStorageInfoProvider, FileSystemStorageInfoProvider>();
    }
}