using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.Estimation;
using FluentAssertions;
using Moq;
using Xunit;

namespace Deadpool.Tests.Infrastructure;

public class RecentBackupSizeEstimatorTests
{
    private readonly Mock<IBackupJobRepository> _repositoryMock;
    private readonly RecentBackupSizeEstimator _estimator;

    public RecentBackupSizeEstimatorTests()
    {
        _repositoryMock = new Mock<IBackupJobRepository>();
        _estimator = new RecentBackupSizeEstimator(_repositoryMock.Object);
    }

    [Fact]
    public async Task EstimateNextBackupSizeAsync_ShouldApplySafetyMargin()
    {
        var databaseName = "TestDB";
        var backupType = BackupType.Full;
        var lastBackupSize = 100L * 1024 * 1024 * 1024; // 100 GB

        var lastBackup = new BackupJob(
            databaseName: databaseName,
            backupType: backupType,
            backupFilePath: "backup.bak");
        lastBackup.MarkAsRunning();
        lastBackup.MarkAsCompleted(lastBackupSize);

        _repositoryMock
            .Setup(r => r.GetLastSuccessfulBackupAsync(databaseName, backupType))
            .ReturnsAsync(lastBackup);

        var estimate = await _estimator.EstimateNextBackupSizeAsync(databaseName, backupType);

        estimate.Should().NotBeNull();
        estimate.Should().Be((long)(lastBackupSize * 1.2m)); // 120 GB with 20% margin
    }

    [Fact]
    public async Task EstimateNextBackupSizeAsync_ShouldReturnNull_WhenNoHistoricalData()
    {
        var databaseName = "TestDB";
        var backupType = BackupType.Full;

        _repositoryMock
            .Setup(r => r.GetLastSuccessfulBackupAsync(databaseName, backupType))
            .ReturnsAsync((BackupJob?)null);

        var estimate = await _estimator.EstimateNextBackupSizeAsync(databaseName, backupType);

        estimate.Should().BeNull();
    }

    [Fact]
    public async Task EstimateNextBackupSizeAsync_ShouldReturnNull_WhenLastBackupHasZeroSize()
    {
        var databaseName = "TestDB";
        var backupType = BackupType.Full;

        var lastBackup = new BackupJob(
            databaseName: databaseName,
            backupType: backupType,
            backupFilePath: "backup.bak");

        _repositoryMock
            .Setup(r => r.GetLastSuccessfulBackupAsync(databaseName, backupType))
            .ReturnsAsync(lastBackup);

        var estimate = await _estimator.EstimateNextBackupSizeAsync(databaseName, backupType);

        estimate.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EstimateNextBackupSizeAsync_ShouldThrow_WhenDatabaseNameEmpty(string databaseName)
    {
        var act = async () => await _estimator.EstimateNextBackupSizeAsync(databaseName, BackupType.Full);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Database name cannot be empty*");
    }

    [Fact]
    public async Task EstimateNextBackupSizeAsync_ShouldUseConservativeMargin()
    {
        // Verify the safety margin is conservative (>=10%)
        var databaseName = "TestDB";
        var backupType = BackupType.Full;
        var lastBackupSize = 1000L;

        var lastBackup = new BackupJob(
            databaseName: databaseName,
            backupType: backupType,
            backupFilePath: "backup.bak");
        lastBackup.MarkAsRunning();
        lastBackup.MarkAsCompleted(lastBackupSize);

        _repositoryMock
            .Setup(r => r.GetLastSuccessfulBackupAsync(databaseName, backupType))
            .ReturnsAsync(lastBackup);

        var estimate = await _estimator.EstimateNextBackupSizeAsync(databaseName, backupType);

        estimate.Should().NotBeNull();
        estimate.Should().BeGreaterThanOrEqualTo((long)(lastBackupSize * 1.1m)); // At least 10% margin
    }
}
