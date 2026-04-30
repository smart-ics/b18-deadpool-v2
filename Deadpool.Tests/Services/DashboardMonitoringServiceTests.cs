using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Deadpool.Tests.Services;

/// <summary>
/// Tests for dashboard monitoring service — reads pre-computed health checks from repos.
/// </summary>
public class DashboardMonitoringServiceTests
{
    private readonly Mock<IBackupJobRepository> _backupJobRepoMock;
    private readonly Mock<IBackupHealthCheckRepository> _backupHealthRepoMock;
    private readonly Mock<IStorageHealthCheckRepository> _storageHealthRepoMock;
    private readonly Mock<IDatabasePulseRepository> _pulseRepoMock;
    private readonly DashboardMonitoringService _service;
    private const string DatabaseName = "TestDB";
    private const string VolumePath = "D:\\Backups";

    public DashboardMonitoringServiceTests()
    {
        _backupJobRepoMock = new Mock<IBackupJobRepository>();
        _backupHealthRepoMock = new Mock<IBackupHealthCheckRepository>();
        _storageHealthRepoMock = new Mock<IStorageHealthCheckRepository>();
        _pulseRepoMock = new Mock<IDatabasePulseRepository>();
        _service = new DashboardMonitoringService(
            _backupJobRepoMock.Object,
            _backupHealthRepoMock.Object,
            _storageHealthRepoMock.Object,
            _pulseRepoMock.Object,
            NullLogger<DashboardMonitoringService>.Instance);
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_WithHealthyBackupCheck_MapsCorrectly()
    {
        // Arrange
        var check = CreateBackupHealthCheck(HealthStatus.Healthy,
            lastFull: DateTime.UtcNow.AddHours(-12),
            lastDiff: DateTime.UtcNow.AddHours(-2),
            lastLog: DateTime.UtcNow.AddMinutes(-15));

        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync(check);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        SetupEmptyJobRepo();

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.DatabaseName.Should().Be(DatabaseName);
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Healthy);
        snapshot.LastBackupStatus.LastFullBackup.Should().Be(check.LastSuccessfulFullBackup);
        snapshot.LastBackupStatus.LastDifferentialBackup.Should().Be(check.LastSuccessfulDifferentialBackup);
        snapshot.LastBackupStatus.LastLogBackup.Should().Be(check.LastSuccessfulLogBackup);
        snapshot.LastBackupStatus.ChainHealthSummary.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_NoBackupHealthData_ReturnsWarningStatus()
    {
        // Arrange
        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        SetupEmptyJobRepo();

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Warning);
        snapshot.LastBackupStatus.Warnings.Should().Contain(x => x.Contains("Agent"));
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_CriticalBackupCheck_MapsHealthCorrectly()
    {
        // Arrange
        var check = CreateBackupHealthCheck(HealthStatus.Critical,
            criticalFindings: new[] { "Full backup overdue" });

        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync(check);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        SetupEmptyJobRepo();

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Critical);
        snapshot.LastBackupStatus.CriticalIssues.Should().Contain("Full backup overdue");
        snapshot.LastBackupStatus.ChainHealthSummary.Should().Be("Critical");
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_WithStorageHealthCheck_MapsCorrectly()
    {
        // Arrange
        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        SetupEmptyJobRepo();

        var storageCheck = CreateStorageHealthCheck(HealthStatus.Critical, freePercentage: 5);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync(storageCheck);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.StorageStatus.OverallHealth.Should().Be(HealthStatus.Critical);
        snapshot.StorageStatus.VolumePath.Should().Be(VolumePath);
        snapshot.StorageStatus.FreePercentage.Should().BeApproximately(5m, 0.1m);
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_NoStorageData_ReturnsWarning()
    {
        // Arrange
        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        SetupEmptyJobRepo();

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.StorageStatus.OverallHealth.Should().Be(HealthStatus.Warning);
        snapshot.StorageStatus.Warnings.Should().Contain(x => x.Contains("Agent"));
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_WithPulseRecord_IncludesInSnapshot()
    {
        // Arrange
        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        SetupEmptyJobRepo();

        var pulseRecord = new DatabasePulseRecord(DateTime.UtcNow.AddSeconds(-30), HealthStatus.Healthy);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync(pulseRecord);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.DatabasePulseStatus.Should().NotBeNull();
        snapshot.DatabasePulseStatus!.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_NoPulseData_SnapshotHasNullPulse()
    {
        // Arrange
        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        SetupEmptyJobRepo();

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.DatabasePulseStatus.Should().BeNull();
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_NoFullBackup_ChainNotInitialized()
    {
        // Arrange
        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        _backupJobRepoMock.Setup(x => x.GetLastSuccessfulFullBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _backupJobRepoMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<BackupJob>());

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.ChainInitializationStatus.IsInitialized.Should().BeFalse();
        snapshot.ChainInitializationStatus.LastValidFullBackupTime.Should().BeNull();
        snapshot.ChainInitializationStatus.WarningMessage.Should().Contain("System not yet protected");
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_FullBackupExists_ChainInitialized()
    {
        // Arrange
        var fullBackup = CreateCompletedBackup(BackupType.Full, hoursAgo: 1);

        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        _backupJobRepoMock.Setup(x => x.GetLastSuccessfulFullBackupAsync(DatabaseName))
            .ReturnsAsync(fullBackup);
        _backupJobRepoMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(new[] { fullBackup });

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.ChainInitializationStatus.IsInitialized.Should().BeTrue();
        snapshot.ChainInitializationStatus.LastValidFullBackupTime.Should().Be(fullBackup.EndTime);
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_JobRepoFails_ChainStatusUnknown()
    {
        // Arrange
        _backupHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(DatabaseName))
            .ReturnsAsync((BackupHealthCheck?)null);
        _storageHealthRepoMock.Setup(x => x.GetLatestHealthCheckAsync(VolumePath))
            .ReturnsAsync((StorageHealthCheck?)null);
        _pulseRepoMock.Setup(x => x.GetLatestAsync())
            .ReturnsAsync((DatabasePulseRecord?)null);
        _backupJobRepoMock.Setup(x => x.GetLastSuccessfulFullBackupAsync(DatabaseName))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable"));
        _backupJobRepoMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<BackupJob>());

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.ChainInitializationStatus.IsInitialized.Should().BeNull();
        snapshot.ChainInitializationStatus.RestoreChainHealth.Should().Be("Unknown");
        snapshot.ChainInitializationStatus.WarningMessage.Should().Contain("Backup status unknown");
    }

    private void SetupEmptyJobRepo()
    {
        _backupJobRepoMock.Setup(x => x.GetLastSuccessfulFullBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _backupJobRepoMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<BackupJob>());
    }

    private static BackupHealthCheck CreateBackupHealthCheck(
        HealthStatus health,
        DateTime? lastFull = null,
        DateTime? lastDiff = null,
        DateTime? lastLog = null,
        string[]? warnings = null,
        string[]? criticalFindings = null)
    {
        return BackupHealthCheck.Restore(
            DatabaseName,
            DateTime.UtcNow,
            health,
            lastFull,
            lastDiff,
            lastLog,
            lastFailedBackup: null,
            warnings?.ToList() ?? new List<string>(),
            criticalFindings?.ToList() ?? new List<string>(),
            limitations: new List<string>());
    }

    private static StorageHealthCheck CreateStorageHealthCheck(HealthStatus health, decimal freePercentage)
    {
        var totalBytes = 1024L * 1024 * 1024 * 1024; // 1 TB
        var freeBytes = (long)(totalBytes * (freePercentage / 100m));

        var warnings = health == HealthStatus.Warning ? new List<string> { "Storage low" } : new List<string>();
        var criticalFindings = health == HealthStatus.Critical ? new List<string> { "Storage critically low" } : new List<string>();

        return StorageHealthCheck.Restore(
            VolumePath, DateTime.UtcNow, totalBytes, freeBytes, health, warnings, criticalFindings);
    }

    private static BackupJob CreateCompletedBackup(BackupType type, int hoursAgo = 0)
    {
        var job = new BackupJob(DatabaseName, type, $"D:\\Backups\\{type}_{Guid.NewGuid()}.bak");
        job.MarkAsRunning();
        job.MarkAsCompleted(1024 * 1024 * 100);

        typeof(BackupJob).GetProperty(nameof(BackupJob.EndTime))!
            .SetValue(job, DateTime.UtcNow.AddHours(-hoursAgo));

        return job;
    }
}
