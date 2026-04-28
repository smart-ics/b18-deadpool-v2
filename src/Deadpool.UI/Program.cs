using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Serilog;
using Deadpool.Infrastructure.Persistence.Repositories;
using Deadpool.Infrastructure.SqlServer;

namespace Deadpool.UI;

internal static class Program
{
    public static ServiceProvider? ServiceProvider { get; private set; }

    [STAThread]
    static void Main()
    {
        // Configure logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("logs/deadpool-ui-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, configuration);
            ServiceProvider = services.BuildServiceProvider();

            // Configure WinForms
            ApplicationConfiguration.Initialize();
            
            // Run main form
            var mainForm = ServiceProvider.GetRequiredService<Forms.MainForm>();
            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            MessageBox.Show($"Fatal error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
            ServiceProvider?.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration
        services.AddSingleton(configuration);

        // Register repositories
        services.AddTransient<Core.Domain.Repositories.ISqlServerInstanceRepository, SqlServerInstanceRepository>();
        services.AddTransient<Core.Domain.Repositories.IDatabaseRepository, DatabaseRepository>();
        services.AddTransient<Core.Domain.Repositories.IBackupJobRepository, BackupJobRepository>();
        services.AddTransient<Core.Domain.Repositories.IBackupScheduleRepository, BackupScheduleRepository>();

        // Register services
        services.AddTransient<Core.Application.Services.IBackupExecutionService, SqlServerBackupExecutionService>();
        services.AddTransient<Core.Application.Services.IMonitoringService, SqlServerMonitoringService>();
        services.AddTransient<Core.Application.Services.ISchedulerService, SchedulerService>();

        // Register forms
        services.AddTransient<Forms.MainForm>();
        services.AddTransient<Forms.ServerManagementForm>();
        services.AddTransient<Forms.BackupScheduleForm>();
        services.AddTransient<Forms.BackupJobMonitorForm>();
    }
}
