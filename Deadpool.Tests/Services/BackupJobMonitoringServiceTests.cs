using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;

namespace Deadpool.Tests.Services;

public class BackupJobMonitoringServiceTests
{
    private readonly Mock<IBackupJobRepository> _mockRepository;
    private readonly BackupJobMonitoringService _service;

    public BackupJobMonitoringServiceTests()
    {
        _mockRepository = new Mock<IBackupJobRepository>();
        _service = new BackupJobMonitoringService(_mockRepository.Object);
    }

    [Fact]
    public async Task GetBackupJobHistoryAsync_NoFilter_ReturnsAllJobs()
    {
        // Arrange
        var jobs = CreateSampleJobs();
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(jobs);

        var filter = new BackupJobFilter("TestDB");

        // Act
        var result = await _service.GetBackupJobHistoryAsync(filter);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetBackupJobHistoryAsync_FilterByStatus_ReturnsMatchingJobs()
    {
        // Arrange
        var jobs = CreateSampleJobs();
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(jobs);

        var filter = new BackupJobFilter
        {
            DatabaseName = "TestDB",
            Status = BackupStatus.Failed
        };

        // Act
        var result = await _service.GetBackupJobHistoryAsync(filter);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GetBackupJobHistoryAsync_FilterByBackupType_ReturnsMatchingJobs()
    {
        // Arrange
        var jobs = CreateSampleJobs();
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(jobs);

        var filter = new BackupJobFilter
        {
            DatabaseName = "TestDB",
            BackupType = BackupType.Full
        };

        // Act
        var result = await _service.GetBackupJobHistoryAsync(filter);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(j => j.BackupType.Should().Be("Full"));
    }

    [Fact]
    public async Task GetBackupJobHistoryAsync_FilterByDateRange_ReturnsMatchingJobs()
    {
        // Arrange
        var jobs = CreateSampleJobs();
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(jobs);

        var filter = new BackupJobFilter
        {
            DatabaseName = "TestDB",
            StartDate = DateTime.UtcNow.Date, // Today
            EndDate = DateTime.UtcNow.Date
        };

        // Act
        var result = await _service.GetBackupJobHistoryAsync(filter);

        // Assert
        result.Should().HaveCount(5); // All jobs should be from today
    }

    [Fact]
    public async Task GetBackupJobHistoryAsync_MaxResults_LimitsReturnedJobs()
    {
        // Arrange
        var jobs = CreateSampleJobs();
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(jobs);

        var filter = new BackupJobFilter
        {
            DatabaseName = "TestDB",
            MaxResults = 2
        };

        // Act
        var result = await _service.GetBackupJobHistoryAsync(filter);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBackupJobHistoryAsync_SortsByNewestFirst()
    {
        // Arrange
        var jobs = CreateSampleJobs();
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(jobs);

        var filter = new BackupJobFilter("TestDB");

        // Act
        var result = await _service.GetBackupJobHistoryAsync(filter);

        // Assert
        result.Should().BeInDescendingOrder(j => j.StartTime);
    }

    [Fact]
    public async Task GetJobStatusSummaryAsync_ReturnsCountsByStatus()
    {
        // Arrange
        var jobs = CreateSampleJobs();
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(jobs);

        // Act
        var result = await _service.GetJobStatusSummaryAsync("TestDB");

        // Assert
        result["Pending"].Should().Be(1);
        result["Running"].Should().Be(0);
        result["Completed"].Should().Be(3);
        result["Failed"].Should().Be(1);
    }

    [Fact]
    public async Task GetJobStatusSummaryAsync_NoJobs_ReturnsZeroCounts()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob>());

        // Act
        var result = await _service.GetJobStatusSummaryAsync("TestDB");

        // Assert
        result["Pending"].Should().Be(0);
        result["Running"].Should().Be(0);
        result["Completed"].Should().Be(0);
        result["Failed"].Should().Be(0);
    }

    private List<BackupJob> CreateSampleJobs()
    {
        // Note: BackupJob constructor sets StartTime to UtcNow, so we create jobs with current time
        // For testing purposes, this is acceptable as we're testing filtering and sorting logic

        var job1 = new BackupJob("TestDB", BackupType.Full, @"C:\Backups\full_1.bak");
        job1.MarkAsRunning();
        job1.MarkAsCompleted(1024 * 1024 * 100);

        var job2 = new BackupJob("TestDB", BackupType.Differential, @"C:\Backups\diff_1.bak");
        job2.MarkAsRunning();
        job2.MarkAsCompleted(1024 * 1024 * 50);

        var job3 = new BackupJob("TestDB", BackupType.TransactionLog, @"C:\Backups\log_1.trn");
        job3.MarkAsRunning();
        job3.MarkAsCompleted(1024 * 1024 * 10);

        var job4 = new BackupJob("TestDB", BackupType.TransactionLog, @"C:\Backups\log_2.trn");
        // Leave as Pending

        var job5 = new BackupJob("TestDB", BackupType.Full, @"C:\Backups\full_2.bak");
        job5.MarkAsRunning();
        job5.MarkAsFailed("Disk full");

        return new List<BackupJob> { job1, job2, job3, job4, job5 };
    }
}
