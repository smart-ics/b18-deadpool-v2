using Deadpool.Agent.Configuration;
using Deadpool.Agent.Infrastructure;
using Deadpool.Agent.Workers;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Deadpool.Tests.Unit;

public class BootstrapWorkerTests
{
    private static BootstrapWorker BuildWorker(
        Mock<IBackupChainInitializationService> initServiceMock,
        InMemoryBootstrapStateTracker stateTracker,
        IScheduleTracker? scheduleTracker = null,
        params string[] databaseNames)
    {
        var policies = databaseNames.Select(db => new DatabaseBackupPolicyOptions
        {
            DatabaseName = db,
            FullBackupCron = "0 12 * * *",
            DifferentialBackupCron = "0 1 * * *",
            TransactionLogBackupCron = "*/15 * * * *"
        }).ToList();

        return new BootstrapWorker(
            NullLogger<BootstrapWorker>.Instance,
            initServiceMock.Object,
            stateTracker,
            scheduleTracker ?? new InMemoryScheduleTracker(),
            Options.Create(policies));
    }

    // ── Fresh install: no backups → bootstrap triggered ───────────────────────────

    [Fact]
    public async Task CheckAndBootstrap_ShouldBootstrap_WhenNoFullBackupExists()
    {
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB")).ReturnsAsync(false);
        initService.Setup(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var tracker = new InMemoryBootstrapStateTracker();
        var worker = BuildWorker(initService, tracker, null, "TestDB");

        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);

        initService.Verify(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>()), Times.Once);
        tracker.GetStatus("TestDB").Should().Be(BackupChainInitializationStatus.Initialized);
    }

    // ── Existing Full backup → no bootstrap ───────────────────────────────────────

    [Fact]
    public async Task CheckAndBootstrap_ShouldNotBootstrap_WhenFullBackupExists()
    {
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB")).ReturnsAsync(true);

        var tracker = new InMemoryBootstrapStateTracker();
        var worker = BuildWorker(initService, tracker, null, "TestDB");

        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);

        initService.Verify(s => s.BootstrapAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        tracker.GetStatus("TestDB").Should().Be(BackupChainInitializationStatus.Initialized);
    }

    // ── Bootstrap failure prevents chain progression ───────────────────────────────

    [Fact]
    public async Task CheckAndBootstrap_ShouldSetBootstrapFailed_WhenBootstrapFails()
    {
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB")).ReturnsAsync(false);
        initService.Setup(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var tracker = new InMemoryBootstrapStateTracker();
        var worker = BuildWorker(initService, tracker, null, "TestDB");

        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);

        tracker.GetStatus("TestDB").Should().Be(BackupChainInitializationStatus.BootstrapFailed);
    }

    // ── Restart does not duplicate bootstrap ──────────────────────────────────────

    [Fact]
    public async Task CheckAndBootstrap_CalledTwice_ShouldOnlyBootstrapOnce_WhenAlreadyInitialized()
    {
        // Simulate: second call reflects that first bootstrap succeeded (repo now has a Full)
        var callCount = 0;
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB"))
            .ReturnsAsync(() => callCount++ > 0); // first call = false, subsequent = true
        initService.Setup(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var tracker = new InMemoryBootstrapStateTracker();
        var worker = BuildWorker(initService, tracker, null, "TestDB");

        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);
        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None); // simulates service restart

        initService.Verify(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Bootstrap exception sets BootstrapFailed ──────────────────────────────────

    [Fact]
    public async Task CheckAndBootstrap_ShouldSetBootstrapFailed_WhenExceptionThrown()
    {
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB")).ReturnsAsync(false);
        initService.Setup(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQL Server unavailable"));

        var tracker = new InMemoryBootstrapStateTracker();
        var worker = BuildWorker(initService, tracker, null, "TestDB");

        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);

        tracker.GetStatus("TestDB").Should().Be(BackupChainInitializationStatus.BootstrapFailed);
    }

    // ── Schedule tracker seeding after bootstrap ──────────────────────────────────

    [Fact]
    public async Task CheckAndBootstrap_ShouldSeedScheduleTracker_AfterSuccessfulBootstrap()
    {
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB")).ReturnsAsync(false);
        initService.Setup(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var stateTracker = new InMemoryBootstrapStateTracker();
        var scheduleTracker = new InMemoryScheduleTracker();
        var worker = BuildWorker(initService, stateTracker, scheduleTracker, "TestDB");

        var before = DateTime.UtcNow;
        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);
        var after = DateTime.UtcNow;

        // Schedule tracker must record Full as executed so the scheduler first tick
        // does not consider Full overdue and create a duplicate job.
        var lastScheduled = scheduleTracker.GetLastScheduled("TestDB", BackupType.Full);
        lastScheduled.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CheckAndBootstrap_ShouldSeedScheduleTracker_WhenAlreadyInitialized()
    {
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB")).ReturnsAsync(true);

        var stateTracker = new InMemoryBootstrapStateTracker();
        var scheduleTracker = new InMemoryScheduleTracker();
        var worker = BuildWorker(initService, stateTracker, scheduleTracker, "TestDB");

        var before = DateTime.UtcNow;
        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);
        var after = DateTime.UtcNow;

        var lastScheduled = scheduleTracker.GetLastScheduled("TestDB", BackupType.Full);
        lastScheduled.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CheckAndBootstrap_ShouldNotSeedScheduleTracker_WhenBootstrapFails()
    {
        var initService = new Mock<IBackupChainInitializationService>();
        initService.Setup(s => s.IsChainInitializedAsync("TestDB")).ReturnsAsync(false);
        initService.Setup(s => s.BootstrapAsync("TestDB", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var stateTracker = new InMemoryBootstrapStateTracker();
        var scheduleTracker = new InMemoryScheduleTracker();
        var worker = BuildWorker(initService, stateTracker, scheduleTracker, "TestDB");

        await worker.CheckAndBootstrapAsync("TestDB", CancellationToken.None);

        // No Full backup completed — tracker must remain at default (MinValue) so
        // the scheduler correctly sees no Full has ever been executed.
        var lastScheduled = scheduleTracker.GetLastScheduled("TestDB", BackupType.Full);
        lastScheduled.Should().Be(DateTime.MinValue);
    }
}
