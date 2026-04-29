using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Deadpool.Tests.Services;

/// <summary>
/// Pragmatic tests for dashboard monitoring service view logic.
/// </summary>
public class DashboardMonitoringServiceTests
{
    private readonly Mock<IBackupJobRepository> _repositoryMock;
    private readonly Mock<IStorageMonitoringService> _storageMock;
    private readonly DashboardMonitoringService _service;
    private const string DatabaseName = "TestDB";
    private const string VolumePath = "D:\\Backups";

    public DashboardMonitoringServiceTests()
    {
        _repositoryMock = new Mock<IBackupJobRepository>();
        _storageMock = new Mock<IStorageMonitoringService>();
        _service = new DashboardMonitoringService(_repositoryMock.Object, _storageMock.Object);
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_ShouldReturnValidSnapshot()
    {
        // Arrange
        var fullBackup = CreateCompletedBackup(BackupType.Full, hoursAgo: 12);
        var diffBackup = CreateCompletedBackup(BackupType.Differential, hoursAgo: 2);
        var logBackup = CreateCompletedBackup(BackupType.TransactionLog, minutesAgo: 15);

        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Full))
            .ReturnsAsync(fullBackup);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Differential))
            .ReturnsAsync(diffBackup);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.TransactionLog))
            .ReturnsAsync(logBackup);
        _repositoryMock.Setup(x => x.GetLastFailedBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(new[] { fullBackup, diffBackup, logBackup });

        var storageHealth = CreateStorageHealth(HealthStatus.Healthy, freePercentage: 40);
        _storageMock.Setup(x => x.CheckStorageHealthAsync(VolumePath))
            .ReturnsAsync(storageHealth);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.DatabaseName.Should().Be(DatabaseName);
        snapshot.LastBackupStatus.Should().NotBeNull();
        snapshot.LastBackupStatus.LastFullBackup.Should().Be(fullBackup.EndTime);
        snapshot.LastBackupStatus.LastDifferentialBackup.Should().Be(diffBackup.EndTime);
        snapshot.LastBackupStatus.LastLogBackup.Should().Be(logBackup.EndTime);
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Healthy);
        snapshot.RecentJobs.Should().HaveCount(3);
        snapshot.StorageStatus.Should().NotBeNull();
        snapshot.StorageStatus.OverallHealth.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_NoFullBackup_ShouldReturnCriticalHealth()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Full))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Differential))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.TransactionLog))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastFailedBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<BackupJob>());

        var storageHealth = CreateStorageHealth(HealthStatus.Healthy, freePercentage: 40);
        _storageMock.Setup(x => x.CheckStorageHealthAsync(VolumePath))
            .ReturnsAsync(storageHealth);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Critical);
        snapshot.LastBackupStatus.CriticalIssues.Should().Contain(x => x.Contains("No Full backup found"));
        snapshot.LastBackupStatus.ChainHealthSummary.Should().Contain("Broken");
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_FullBackupOverdue_ShouldReturnCritical()
    {
        // Arrange - Full backup over 72 hours old
        var fullBackup = CreateCompletedBackup(BackupType.Full, hoursAgo: 80);

        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Full))
            .ReturnsAsync(fullBackup);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Differential))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.TransactionLog))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastFailedBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(new[] { fullBackup });

        var storageHealth = CreateStorageHealth(HealthStatus.Healthy, freePercentage: 40);
        _storageMock.Setup(x => x.CheckStorageHealthAsync(VolumePath))
            .ReturnsAsync(storageHealth);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Critical);
        snapshot.LastBackupStatus.CriticalIssues.Should().Contain(x => x.Contains("hours old") && x.Contains(">72h"));
        snapshot.LastBackupStatus.ChainHealthSummary.Should().Contain("Critical");
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_FullBackupAging_ShouldReturnWarning()
    {
        // Arrange - Full backup between 48-72 hours old
        var fullBackup = CreateCompletedBackup(BackupType.Full, hoursAgo: 50);

        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Full))
            .ReturnsAsync(fullBackup);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Differential))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.TransactionLog))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastFailedBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(new[] { fullBackup });

        var storageHealth = CreateStorageHealth(HealthStatus.Healthy, freePercentage: 40);
        _storageMock.Setup(x => x.CheckStorageHealthAsync(VolumePath))
            .ReturnsAsync(storageHealth);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Warning);
        snapshot.LastBackupStatus.Warnings.Should().Contain(x => x.Contains("hours old") && x.Contains(">48h"));
        snapshot.LastBackupStatus.ChainHealthSummary.Should().Contain("Warning");
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_LogBackupStale_ShouldReturnWarning()
    {
        // Arrange - Log backup over 60 minutes old
        var fullBackup = CreateCompletedBackup(BackupType.Full, hoursAgo: 12);
        var logBackup = CreateCompletedBackup(BackupType.TransactionLog, minutesAgo: 90);

        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Full))
            .ReturnsAsync(fullBackup);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Differential))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.TransactionLog))
            .ReturnsAsync(logBackup);
        _repositoryMock.Setup(x => x.GetLastFailedBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(new[] { fullBackup, logBackup });

        var storageHealth = CreateStorageHealth(HealthStatus.Healthy, freePercentage: 40);
        _storageMock.Setup(x => x.CheckStorageHealthAsync(VolumePath))
            .ReturnsAsync(storageHealth);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.LastBackupStatus.OverallHealth.Should().Be(HealthStatus.Warning);
        snapshot.LastBackupStatus.Warnings.Should().Contain(x => x.Contains("Log backup") && x.Contains("minutes old"));
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_RecentFailure_ShouldIncludeWarning()
    {
        // Arrange
        var fullBackup = CreateCompletedBackup(BackupType.Full, hoursAgo: 12);
        var failedBackup = CreateFailedBackup(BackupType.TransactionLog, hoursAgo: 2, "Disk full");

        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Full))
            .ReturnsAsync(fullBackup);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Differential))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.TransactionLog))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastFailedBackupAsync(DatabaseName))
            .ReturnsAsync(failedBackup);
        _repositoryMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(new[] { fullBackup, failedBackup });

        var storageHealth = CreateStorageHealth(HealthStatus.Healthy, freePercentage: 40);
        _storageMock.Setup(x => x.CheckStorageHealthAsync(VolumePath))
            .ReturnsAsync(storageHealth);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.LastBackupStatus.Warnings.Should().Contain(x => x.Contains("Recent failure") && x.Contains("Disk full"));
    }

    [Fact]
    public async Task GetDashboardSnapshotAsync_StorageCritical_ShouldReflectInSnapshot()
    {
        // Arrange
        var fullBackup = CreateCompletedBackup(BackupType.Full, hoursAgo: 12);

        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Full))
            .ReturnsAsync(fullBackup);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.Differential))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastSuccessfulBackupAsync(DatabaseName, BackupType.TransactionLog))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetLastFailedBackupAsync(DatabaseName))
            .ReturnsAsync((BackupJob?)null);
        _repositoryMock.Setup(x => x.GetRecentJobsAsync(DatabaseName, It.IsAny<int>()))
            .ReturnsAsync(new[] { fullBackup });

        var storageHealth = CreateStorageHealth(HealthStatus.Critical, freePercentage: 5);
        _storageMock.Setup(x => x.CheckStorageHealthAsync(VolumePath))
            .ReturnsAsync(storageHealth);

        // Act
        var snapshot = await _service.GetDashboardSnapshotAsync(DatabaseName, VolumePath);

        // Assert
        snapshot.StorageStatus.OverallHealth.Should().Be(HealthStatus.Critical);
        snapshot.StorageStatus.FreePercentage.Should().Be(5);
    }

    private BackupJob CreateCompletedBackup(BackupType type, int hoursAgo = 0, int minutesAgo = 0)
    {
        var startTime = DateTime.UtcNow.AddHours(-hoursAgo).AddMinutes(-minutesAgo).AddMinutes(-5);
        var job = new BackupJob(DatabaseName, type, $"D:\\Backups\\{type}_{Guid.NewGuid()}.bak");

        // Simulate job lifecycle
        typeof(BackupJob).GetProperty(nameof(BackupJob.StartTime))!
            .SetValue(job, startTime);

        job.MarkAsRunning();
        job.MarkAsCompleted(1024 * 1024 * 100); // 100 MB

        // Fix EndTime to match the desired age
        typeof(BackupJob).GetProperty(nameof(BackupJob.EndTime))!
            .SetValue(job, DateTime.UtcNow.AddHours(-hoursAgo).AddMinutes(-minutesAgo));

        return job;
    }

    private BackupJob CreateFailedBackup(BackupType type, int hoursAgo, string errorMessage)
    {
        var startTime = DateTime.UtcNow.AddHours(-hoursAgo);
        var job = new BackupJob(DatabaseName, type, $"D:\\Backups\\{type}_{Guid.NewGuid()}.bak");

        typeof(BackupJob).GetProperty(nameof(BackupJob.StartTime))!
            .SetValue(job, startTime);

        job.MarkAsRunning();
        job.MarkAsFailed(errorMessage);

        return job;
    }

    private StorageHealthCheck CreateStorageHealth(HealthStatus health, decimal freePercentage)
    {
        var storageHealth = new StorageHealthCheck(VolumePath);
        var totalBytes = 1024L * 1024 * 1024 * 1024; // 1 TB
        var freeBytes = (long)(totalBytes * (freePercentage / 100m));

        storageHealth.RecordStorageMetrics(totalBytes, freeBytes);

        if (health == HealthStatus.Warning)
        {
            storageHealth.AddWarning("Storage low");
        }
        else if (health == HealthStatus.Critical)
        {
            storageHealth.AddCriticalFinding("Storage critically low");
        }

        return storageHealth;
    }
}
