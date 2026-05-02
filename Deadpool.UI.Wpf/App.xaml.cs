using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using Deadpool.Core.Configuration;
using Deadpool.Infrastructure.Persistence;
using Deadpool.UI.Wpf.Views;
using Deadpool.UI.Wpf.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows;

namespace Deadpool.UI.Wpf;

public partial class App : Application
{
	public static IServiceProvider Services { get; private set; } = null!;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

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

		var services = new ServiceCollection();

		ConfigureServices(services, configuration);
		var serviceProvider = services.BuildServiceProvider();
		Services = serviceProvider;

		var mainWindow = Services.GetRequiredService<MainWindow>();
		mainWindow.Show();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (Services is IDisposable disposable)
		{
			disposable.Dispose();
		}

		base.OnExit(e);
	}

	private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton(configuration);
		var firstDatabaseName = configuration
			.GetSection("BackupPolicies")
			.GetChildren()
			.FirstOrDefault()?["DatabaseName"];
		services.Configure<RestoreOrchestratorOptions>(options =>
		{
			options.DatabaseName = firstDatabaseName ?? string.Empty;
			options.AllowOverwrite = bool.TryParse(configuration["Restore:StartupCommand:AllowOverwrite"], out var allowOverwrite)
				&& allowOverwrite;
		});

		services.Configure<RestoreExecutionOptions>(options =>
		{
			options.ConnectionString = configuration.GetConnectionString("ProductionDatabase") ?? string.Empty;
			options.CommandTimeoutSeconds = int.TryParse(configuration["Restore:Execution:CommandTimeoutSeconds"], out var timeoutSeconds)
				? timeoutSeconds
				: 300;
		});

		services.AddLogging(builder =>
		{
			builder.AddDebug();
			builder.SetMinimumLevel(LogLevel.Information);
		});

		var sqlitePath = configuration["DeadpoolDb:Path"];
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

		services.AddSingleton<IDashboardMonitoringService, DashboardMonitoringService>();
		services.AddScoped<IRestorePlannerService, RestorePlannerService>();
		services.AddScoped<IRestorePlanValidatorService, RestorePlanValidatorService>();
		services.AddScoped<IRestoreScriptBuilderService, RestoreScriptBuilderService>();
		services.AddScoped<IRestoreSafetyGuard, RestoreSafetyGuardService>();
		services.AddScoped<IRestoreExecutionService, RestoreExecutionService>();
		services.AddScoped<IRestoreOrchestratorService, RestoreOrchestratorService>();
		services.AddTransient<RestoreViewModel>();
		services.AddTransient<RestoreWindow>();
		services.AddSingleton<MainWindow>(sp =>
			new MainWindow(
				sp.GetRequiredService<DashboardViewModel>(),
				sp));

		services.AddSingleton<DashboardViewModel>(sp =>
		{
			var databaseName = configuration
				.GetSection("BackupPolicies")
				.GetChildren()
				.FirstOrDefault()?["DatabaseName"]
				?? "UNKNOWN";

			var backupVolumePath = configuration["BackupStorage:StorageFolder"] ?? string.Empty;

			return new DashboardViewModel(
				sp.GetRequiredService<IDashboardMonitoringService>(),
				sp.GetRequiredService<IAgentHeartbeatRepository>(),
				sp.GetRequiredService<ILogger<DashboardViewModel>>(),
				databaseName,
				backupVolumePath);
		});
	}
}
