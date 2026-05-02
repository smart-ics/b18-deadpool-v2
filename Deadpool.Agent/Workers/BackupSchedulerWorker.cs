using Deadpool.Agent.Configuration;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Options;
using Deadpool.Agent.Infrastructure;

namespace Deadpool.Agent.Workers;

// Polls every minute. For each configured database/backup-type pair it checks whether
// a cron occurrence has fallen due since the last time a job was scheduled, and if so
// creates a pending BackupJob.  Execution is intentionally NOT performed here —
// BackupExecutorWorker (P0-006) picks up pending jobs.
public sealed class BackupSchedulerWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<BackupSchedulerWorker> _logger;
    private readonly IBackupJobRepository _jobRepository;
    private readonly IScheduleTracker _tracker;
    private readonly IBootstrapStateTracker _bootstrapStateTracker;
    private readonly IReadOnlyList<ScheduledDatabase> _databases;

    public BackupSchedulerWorker(
        ILogger<BackupSchedulerWorker> logger,
        IBackupJobRepository jobRepository,
        IScheduleTracker tracker,
        IBootstrapStateTracker bootstrapStateTracker,
        IOptions<List<DatabaseBackupPolicyOptions>> policiesOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _bootstrapStateTracker = bootstrapStateTracker ?? throw new ArgumentNullException(nameof(bootstrapStateTracker));

        var raw = policiesOptions?.Value
            ?? throw new ArgumentNullException(nameof(policiesOptions));

        _databases = raw.Select(ScheduledDatabase.From).ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BackupSchedulerWorker started. Monitoring {Count} database(s).", _databases.Count);

        // On start-up, seed the tracker from the job repository so that a restart
        // doesn't reschedule backups that were already created.
        await SeedTrackerFromRepositoryAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await TickAsync(DateTime.Now, stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("BackupSchedulerWorker stopped.");
    }

    // Seeding: for each (db, type) query the most recent job and restore the tracker
    // so the first tick does not create duplicates.
    private async Task SeedTrackerFromRepositoryAsync(CancellationToken ct)
    {
        foreach (var db in _databases)
        {
            foreach (var (backupType, _) in db.Schedules)
            {
                if (ct.IsCancellationRequested) return;

                try
                {
                    var recent = await _jobRepository.GetRecentJobsAsync(db.DatabaseName, count: 1);
                    var last = recent.FirstOrDefault(j => j.BackupType == backupType);
                    if (last != null)
                    {
                        _tracker.MarkScheduled(db.DatabaseName, backupType, last.StartTime);
                        _logger.LogDebug(
                            "Seeded tracker {Db}/{Type} from repository: {Time:u}",
                            db.DatabaseName, backupType, last.StartTime);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not seed tracker for {Db}/{Type}. Will re-schedule if due.",
                        db.DatabaseName, backupType);
                }
            }
        }
    }

    // Main tick — separated from the loop for testability.
    internal async Task TickAsync(DateTime now, CancellationToken ct)
    {
        foreach (var db in _databases)
        {
            if (ct.IsCancellationRequested) return;

            foreach (var (backupType, schedule) in db.Schedules)
            {
                if (ct.IsCancellationRequested) return;

                try
                {
                    await TryScheduleAsync(db.DatabaseName, backupType, schedule, now);
                }
                catch (Exception ex)
                {
                    // Isolate failures per (db, type) — a broken log schedule must not
                    // block the full backup schedule for another database.
                    _logger.LogError(ex,
                        "Unexpected error while checking schedule for {Db}/{Type}.",
                        db.DatabaseName, backupType);
                }
            }
        }
    }

    private async Task TryScheduleAsync(
        string databaseName,
        BackupType backupType,
        BackupSchedule schedule,
        DateTime now)
    {
        // Domain safety rule: Differential and Log backups require an initialized chain.
        if (backupType != BackupType.Full)
        {
            var chainStatus = _bootstrapStateTracker.GetStatus(databaseName);
            if (chainStatus != BackupChainInitializationStatus.Initialized)
            {
                _logger.LogWarning(
                    "Skipping {Type} backup for {Db}: chain not initialized (status={Status}).",
                    backupType, databaseName, chainStatus);
                return;
            }
        }

        var lastScheduled = _tracker.GetLastScheduled(databaseName, backupType);

        if (!schedule.IsDue(lastScheduled, now))
            return;

        // Use the most recent occurrence as the canonical anchor, not necessarily the
        // first one.  This ensures repeated ticks within the same inter-occurrence
        // window always advance the tracker to the same point, preventing duplicates.
        var occurrence = schedule.GetMostRecentOccurrence(lastScheduled, now)!.Value;

        _logger.LogInformation(
            "Scheduling {Type} backup for {Db} (occurrence {Occurrence}).",
            backupType, databaseName, occurrence);

        var job = new BackupJob(
            databaseName,
            backupType,
            BuildPlaceholderPath(databaseName, backupType, occurrence));

        await _jobRepository.CreateAsync(job);

        // Update tracker only after the job was successfully persisted.
        _tracker.MarkScheduled(databaseName, backupType, occurrence);

        _logger.LogInformation(
            "Scheduled {Type} backup job created for {Db}.", backupType, databaseName);
    }

    // The real file path is assigned by BackupExecutorWorker (P0-006) when it picks
    // up the pending job. We write a recognisable placeholder here.
    private static string BuildPlaceholderPath(
        string databaseName, BackupType backupType, DateTime occurrence)
    {
        var ext = backupType == BackupType.TransactionLog ? "trn" : "bak";
        var tag = backupType switch
        {
            BackupType.Full => "FULL",
            BackupType.Differential => "DIFF",
            BackupType.TransactionLog => "LOG",
            _ => "UNKNOWN"
        };
        return $"PENDING_{databaseName}_{tag}_{occurrence:yyyyMMdd_HHmm}.{ext}";
    }

    // Internal value type used only by this worker to pair each schedule with its type.
    private sealed record ScheduledDatabase(
        string DatabaseName,
        IReadOnlyList<(BackupType Type, BackupSchedule Schedule)> Schedules)
    {
        internal static ScheduledDatabase From(DatabaseBackupPolicyOptions opts)
        {
            var schedules = new List<(BackupType, BackupSchedule)>();

            if (!string.IsNullOrWhiteSpace(opts.FullBackupCron))
                schedules.Add((BackupType.Full, new BackupSchedule(opts.FullBackupCron)));

            if (!string.IsNullOrWhiteSpace(opts.DifferentialBackupCron))
                schedules.Add((BackupType.Differential, new BackupSchedule(opts.DifferentialBackupCron)));

            if (!string.IsNullOrWhiteSpace(opts.TransactionLogBackupCron))
                schedules.Add((BackupType.TransactionLog, new BackupSchedule(opts.TransactionLogBackupCron)));

            return new ScheduledDatabase(opts.DatabaseName, schedules);
        }
    }
}
