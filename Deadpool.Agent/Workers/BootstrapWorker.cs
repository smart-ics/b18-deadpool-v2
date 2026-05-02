using Deadpool.Agent.Configuration;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deadpool.Agent.Workers;

/// <summary>
/// Runs once at startup for each configured database.
/// If no valid Full backup exists the worker executes a single bootstrap Full backup,
/// then marks the chain Initialized so that <see cref="BackupSchedulerWorker"/> can
/// allow Differential and Log backups to proceed.
///
/// If bootstrap fails the chain status is set to BootstrapFailed and Differential /
/// Log backups remain blocked for that database.
///
/// The worker never runs a second bootstrap for the same database in the same process
/// lifetime; on service restart it re-checks the repository before deciding.
/// </summary>
public sealed class BootstrapWorker : BackgroundService
{
    private readonly ILogger<BootstrapWorker> _logger;
    private readonly IBackupChainInitializationService _initService;
    private readonly IBootstrapStateTracker _stateTracker;
    private readonly IScheduleTracker _scheduleTracker;
    private readonly IReadOnlyList<string> _databaseNames;

    public BootstrapWorker(
        ILogger<BootstrapWorker> logger,
        IBackupChainInitializationService initService,
        IBootstrapStateTracker stateTracker,
        IScheduleTracker scheduleTracker,
        IOptions<List<DatabaseBackupPolicyOptions>> policiesOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _initService = initService ?? throw new ArgumentNullException(nameof(initService));
        _stateTracker = stateTracker ?? throw new ArgumentNullException(nameof(stateTracker));
        _scheduleTracker = scheduleTracker ?? throw new ArgumentNullException(nameof(scheduleTracker));

        var raw = policiesOptions?.Value ?? throw new ArgumentNullException(nameof(policiesOptions));
        _databaseNames = raw
            .Where(p => !string.IsNullOrWhiteSpace(p.DatabaseName))
            .Select(p => p.DatabaseName)
            .ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BootstrapWorker started. Checking {Count} database(s).", _databaseNames.Count);

        foreach (var databaseName in _databaseNames)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            await CheckAndBootstrapAsync(databaseName, stoppingToken);
        }

        _logger.LogInformation("BootstrapWorker finished initialization checks.");
    }

    // Visible for testing.
    internal async Task CheckAndBootstrapAsync(string databaseName, CancellationToken cancellationToken)
    {
        try
        {
            var isInitialized = await _initService.IsChainInitializedAsync(databaseName);

            if (isInitialized)
            {
                _logger.LogInformation(
                    "Bootstrap [{Database}]: chain already initialized — Full backup exists.", databaseName);
                _stateTracker.SetStatus(databaseName, BackupChainInitializationStatus.Initialized);
                // Seed the schedule tracker so the scheduler does not treat Full as overdue
                // on its first tick. SeedTrackerFromRepository in BackupSchedulerWorker also
                // does this from the repository, but racing against it is safe: both writes
                // record a recent time, whichever lands last still prevents a duplicate job.
                _scheduleTracker.MarkScheduled(databaseName, BackupType.Full, DateTime.UtcNow);
                return;
            }

            _logger.LogWarning(
                "Bootstrap [{Database}]: no Full backup found. Starting bootstrap Full backup.", databaseName);
            _stateTracker.SetStatus(databaseName, BackupChainInitializationStatus.BootstrapPending);

            var success = await _initService.BootstrapAsync(databaseName, cancellationToken);

            if (success)
            {
                _stateTracker.SetStatus(databaseName, BackupChainInitializationStatus.Initialized);
                // Prevent the scheduler from queuing a second Full backup job on its first
                // tick. Bootstrap executed the Full directly (outside the job queue); marking
                // it as scheduled now makes the cron anchor current so the next cron
                // occurrence, not the current one, will be the first scheduler-driven Full.
                _scheduleTracker.MarkScheduled(databaseName, BackupType.Full, DateTime.UtcNow);
                _logger.LogInformation(
                    "Bootstrap [{Database}]: chain initialized successfully.", databaseName);
            }
            else
            {
                _stateTracker.SetStatus(databaseName, BackupChainInitializationStatus.BootstrapFailed);
                _logger.LogError(
                    "Bootstrap [{Database}]: bootstrap failed. Differential and Log backups are blocked.", databaseName);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Bootstrap [{Database}]: cancelled during initialization.", databaseName);
            throw;
        }
        catch (Exception ex)
        {
            _stateTracker.SetStatus(databaseName, BackupChainInitializationStatus.BootstrapFailed);
            _logger.LogError(ex,
                "Bootstrap [{Database}]: unexpected error during initialization. Differential and Log backups are blocked.",
                databaseName);
        }
    }
}
