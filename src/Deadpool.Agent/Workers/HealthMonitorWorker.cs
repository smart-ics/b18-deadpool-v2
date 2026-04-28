using Deadpool.Core.Application.Services;
using Deadpool.Core.Domain.Repositories;

namespace Deadpool.Agent.Workers;

/// <summary>
/// Worker service that monitors SQL Server instances and database health
/// </summary>
public class HealthMonitorWorker : BackgroundService
{
    private readonly ILogger<HealthMonitorWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public HealthMonitorWorker(
        ILogger<HealthMonitorWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health Monitor Worker starting at: {time}", DateTimeOffset.Now);

        // Wait a bit before first check to allow other workers to initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorHealthAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Health Monitor Worker: {Message}", ex.Message);
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Health Monitor Worker stopping at: {time}", DateTimeOffset.Now);
    }

    private async Task MonitorHealthAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var serverRepo = scope.ServiceProvider.GetRequiredService<ISqlServerInstanceRepository>();
        var databaseRepo = scope.ServiceProvider.GetRequiredService<IDatabaseRepository>();
        var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

        var servers = await serverRepo.GetAllActiveAsync(cancellationToken);

        foreach (var server in servers)
        {
            try
            {
                // Check server connectivity
                var connectivityResult = await monitoringService.CheckServerConnectivityAsync(server, cancellationToken);
                
                if (connectivityResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Server {Server} is unreachable: {Error}",
                        server.GetFullServerName(),
                        connectivityResult.Error);
                    continue;
                }

                // Update server info
                var serverInfoResult = await monitoringService.GetServerInfoAsync(server, cancellationToken);
                if (serverInfoResult.IsSuccess)
                {
                    server.UpdateServerInfo(serverInfoResult.Value.Version, serverInfoResult.Value.Edition);
                    await serverRepo.UpdateAsync(server, cancellationToken);
                }

                // Check databases
                var databases = await databaseRepo.GetByServerInstanceAsync(server.Id, cancellationToken);
                foreach (var database in databases)
                {
                    var healthResult = await monitoringService.CheckBackupHealthAsync(database, server, cancellationToken);
                    
                    if (healthResult.IsSuccess)
                    {
                        var health = healthResult.Value;
                        
                        if (!health.IsHealthy)
                        {
                            _logger.LogWarning(
                                "Database {Database} on {Server} has backup health issues. Errors: {Errors}",
                                database.Name,
                                server.GetFullServerName(),
                                string.Join(", ", health.Errors));
                        }

                        if (health.Warnings.Any())
                        {
                            _logger.LogInformation(
                                "Database {Database} on {Server} has backup warnings: {Warnings}",
                                database.Name,
                                server.GetFullServerName(),
                                string.Join(", ", health.Warnings));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error monitoring server {Server}: {Message}", 
                    server.GetFullServerName(), 
                    ex.Message);
            }
        }
    }
}
