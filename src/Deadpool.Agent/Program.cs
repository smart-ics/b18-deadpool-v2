using Deadpool.Infrastructure.Persistence.Repositories;
using Deadpool.Infrastructure.SqlServer;
using Serilog;

namespace Deadpool.Agent;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/deadpool-agent-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting Deadpool Agent Service");
            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Deadpool Agent Service terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "Deadpool Agent";
            })
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                // Register repositories
                services.AddTransient<Core.Domain.Repositories.ISqlServerInstanceRepository, SqlServerInstanceRepository>();
                services.AddTransient<Core.Domain.Repositories.IDatabaseRepository, DatabaseRepository>();
                services.AddTransient<Core.Domain.Repositories.IBackupJobRepository, BackupJobRepository>();
                services.AddTransient<Core.Domain.Repositories.IBackupScheduleRepository, BackupScheduleRepository>();

                // Register services
                services.AddTransient<Core.Application.Services.IBackupExecutionService, SqlServerBackupExecutionService>();
                services.AddTransient<Core.Application.Services.IMonitoringService, SqlServerMonitoringService>();
                services.AddTransient<Core.Application.Services.ISchedulerService, SchedulerService>();

                // Register hosted services (workers)
                services.AddHostedService<Workers.BackupSchedulerWorker>();
                services.AddHostedService<Workers.BackupExecutionWorker>();
                services.AddHostedService<Workers.HealthMonitorWorker>();
            });
}
