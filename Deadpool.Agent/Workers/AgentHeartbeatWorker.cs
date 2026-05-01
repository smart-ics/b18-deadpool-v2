using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Deadpool.Agent.Workers;

public sealed class AgentHeartbeatWorker : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<AgentHeartbeatWorker> _logger;
    private readonly IAgentHeartbeatRepository _repository;

    public AgentHeartbeatWorker(
        ILogger<AgentHeartbeatWorker> logger,
        IAgentHeartbeatRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Heartbeat Worker starting. Interval: {Interval}", HeartbeatInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _repository.UpsertHeartbeatAsync(DateTime.UtcNow);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upsert agent heartbeat.");
            }

            try
            {
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Agent Heartbeat Worker stopping.");
    }
}