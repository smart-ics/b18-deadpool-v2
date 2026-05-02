using Deadpool.Agent.Configuration;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deadpool.Agent.Workers;

public sealed class DatabasePulseWorker : BackgroundService
{
    private readonly ILogger<DatabasePulseWorker> _logger;
    private readonly IDatabaseConnectivityProbe _probe;
    private readonly IDatabasePulseRepository _repository;
    private readonly DatabasePulseOptions _options;
    private int _checkCounter = 0;

    public DatabasePulseWorker(
        ILogger<DatabasePulseWorker> logger,
        IDatabaseConnectivityProbe probe,
        IDatabasePulseRepository repository,
        IOptions<DatabasePulseOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Database Pulse Worker starting. Check interval: {Interval}", _options.CheckInterval);

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformPulseCheckAsync(stoppingToken);

                _checkCounter++;
                if (_checkCounter % 60 == 0)
                {
                    PerformRetentionCleanup();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in database pulse worker loop.");
            }

            try
            {
                await Task.Delay(_options.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Database Pulse Worker stopping.");
    }

    private async Task PerformPulseCheckAsync(CancellationToken cancellationToken)
    {
        var checkTime = DateTime.Now;
        try
        {
            await _probe.ProbeAsync(cancellationToken);
            await _repository.CreateAsync(new DatabasePulseRecord(checkTime, HealthStatus.Healthy));
            _logger.LogDebug("Database pulse: Healthy at {CheckTime}", checkTime);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _repository.CreateAsync(new DatabasePulseRecord(checkTime, HealthStatus.Critical, ex.Message));
            _logger.LogWarning("Database pulse: Critical at {CheckTime}. Error: {Message}", checkTime, ex.Message);
        }
    }

    private void PerformRetentionCleanup()
    {
        try
        {
            _repository.CleanupOldRecords(TimeSpan.FromDays(_options.RetentionDays));
            _logger.LogDebug("Database pulse retention cleanup completed.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to perform database pulse retention cleanup.");
        }
    }
}
