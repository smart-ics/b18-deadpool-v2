using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Deadpool.Tests.Services;

public class StorageMonitoringServiceTests
{
    private readonly Mock<IStorageInfoProvider> _storageInfoProviderMock;
    private readonly Mock<IBackupSizeEstimator> _backupSizeEstimatorMock;
    private readonly StorageHealthOptions _options;
    private readonly StorageMonitoringService _service;

    public StorageMonitoringServiceTests()
    {
        _storageInfoProviderMock = new Mock<IStorageInfoProvider>();
        _backupSizeEstimatorMock = new Mock<IBackupSizeEstimator>();
        _options = new StorageHealthOptions(
            warningThresholdPercentage: 20m,
            criticalThresholdPercentage: 10m,
            minimumWarningFreeSpaceBytes: 50L * 1024 * 1024 * 1024, // 50 GB
            minimumCriticalFreeSpaceBytes: 20L * 1024 * 1024 * 1024); // 20 GB
        _service = new StorageMonitoringService(
            _storageInfoProviderMock.Object,
            _options,
            _backupSizeEstimatorMock.Object);
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldReturnHealthy_WhenAboveWarningThreshold()
    {
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 250L * 1024 * 1024 * 1024)); // 1 TB total, 250 GB free (25%)

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeTrue();
        result.FreePercentage.Should().Be(25m);
        result.Warnings.Should().BeEmpty();
        result.CriticalFindings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldReturnWarning_WhenBelowWarningThresholdPercentage()
    {
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 150L * 1024 * 1024 * 1024)); // 1 TB total, 150 GB free (15%)

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeFalse();
        result.FreePercentage.Should().Be(15m);
        result.Warnings.Should().ContainSingle();
        result.Warnings.First().Should().Contain("Low storage space");
        result.Warnings.First().Should().Contain("15.0% free");
        result.CriticalFindings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldReturnWarning_WhenBelowWarningThresholdAbsolute()
    {
        // Small volume: 200 GB total, 45 GB free (22.5% - above percentage threshold)
        // But below absolute 50 GB threshold
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((200L * 1024 * 1024 * 1024, 45L * 1024 * 1024 * 1024));

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeFalse();
        result.FreePercentage.Should().Be(22.5m);
        result.Warnings.Should().ContainSingle();
        result.Warnings.First().Should().Contain("Low storage space");
        result.Warnings.First().Should().Contain("absolute");
        result.CriticalFindings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldReturnCritical_WhenBelowCriticalThresholdPercentage()
    {
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 50L * 1024 * 1024 * 1024)); // 1 TB total, 50 GB free (5%)

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeFalse();
        result.FreePercentage.Should().Be(5m);
        result.CriticalFindings.Should().ContainSingle();
        result.CriticalFindings.First().Should().Contain("Critically low storage space");
        result.CriticalFindings.First().Should().Contain("5.0% free");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldReturnCritical_WhenBelowCriticalThresholdAbsolute()
    {
        // Small volume: 100 GB total, 15 GB free (15% - above critical percentage threshold)
        // But below absolute 20 GB critical threshold
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((100L * 1024 * 1024 * 1024, 15L * 1024 * 1024 * 1024));

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeFalse();
        result.FreePercentage.Should().Be(15m);
        result.CriticalFindings.Should().ContainSingle();
        result.CriticalFindings.First().Should().Contain("Critically low storage space");
        result.CriticalFindings.First().Should().Contain("absolute");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldApplyHysteresis_PreventOscillation()
    {
        // Test that values just above warning threshold are healthy (with hysteresis buffer)
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);

        // At exactly 20% warning threshold - should trigger warning
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 200L * 1024 * 1024 * 1024)); // 20% free

        var result1 = await _service.CheckStorageHealthAsync(volumePath);
        result1.IsHealthy().Should().BeFalse();
        result1.Warnings.Should().ContainSingle();

        // At 22% (2% above warning, but below 3% hysteresis buffer) - should still be healthy
        // because we need to exceed warning + buffer to avoid oscillation
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 220L * 1024 * 1024 * 1024)); // 22% free

        var result2 = await _service.CheckStorageHealthAsync(volumePath);
        result2.IsHealthy().Should().BeTrue(); // Above warning threshold, so healthy

        // At 24% (above warning + 3% buffer) - definitely healthy
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 240L * 1024 * 1024 * 1024)); // 24% free

        var result3 = await _service.CheckStorageHealthAsync(volumePath);
        result3.IsHealthy().Should().BeTrue();
    }

    [Fact]
    public async Task CheckStorageHealthAsync_WithBackupContext_ShouldWarn_WhenNextBackupWillNotFit()
    {
        var volumePath = "C:\\Backups";
        var databaseName = "TestDB";
        var nextBackupType = BackupType.Full;

        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 250L * 1024 * 1024 * 1024)); // 250 GB free (healthy)

        // Estimated backup size: 220 GB
        // After backup: 250 GB - 220 GB = 30 GB remaining (below 50 GB warning threshold)
        _backupSizeEstimatorMock
            .Setup(e => e.EstimateNextBackupSizeAsync(databaseName, nextBackupType))
            .ReturnsAsync(220L * 1024 * 1024 * 1024);

        var result = await _service.CheckStorageHealthAsync(volumePath, databaseName, nextBackupType);

        result.IsHealthy().Should().BeFalse();
        result.Warnings.Should().ContainSingle();
        result.Warnings.First().Should().Contain($"Next {nextBackupType} backup");
        result.Warnings.First().Should().Contain(databaseName);
        result.Warnings.First().Should().Contain("will leave");
    }

    [Fact]
    public async Task CheckStorageHealthAsync_WithBackupContext_ShouldBeCritical_WhenNextBackupWillNotFit()
    {
        var volumePath = "C:\\Backups";
        var databaseName = "TestDB";
        var nextBackupType = BackupType.Full;

        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 250L * 1024 * 1024 * 1024)); // 250 GB free (healthy)

        // Estimated backup size: 240 GB
        // After backup: 250 GB - 240 GB = 10 GB remaining (below 20 GB critical threshold)
        _backupSizeEstimatorMock
            .Setup(e => e.EstimateNextBackupSizeAsync(databaseName, nextBackupType))
            .ReturnsAsync(240L * 1024 * 1024 * 1024);

        var result = await _service.CheckStorageHealthAsync(volumePath, databaseName, nextBackupType);

        result.IsHealthy().Should().BeFalse();
        result.CriticalFindings.Should().ContainSingle();
        result.CriticalFindings.First().Should().Contain($"Next {nextBackupType} backup");
        result.CriticalFindings.First().Should().Contain(databaseName);
        result.CriticalFindings.First().Should().Contain("will leave");
    }

    [Fact]
    public async Task CheckStorageHealthAsync_WithBackupContext_ShouldNotWarn_WhenNoEstimateAvailable()
    {
        var volumePath = "C:\\Backups";
        var databaseName = "TestDB";
        var nextBackupType = BackupType.Full;

        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 250L * 1024 * 1024 * 1024));

        // No historical data available
        _backupSizeEstimatorMock
            .Setup(e => e.EstimateNextBackupSizeAsync(databaseName, nextBackupType))
            .ReturnsAsync((long?)null);

        var result = await _service.CheckStorageHealthAsync(volumePath, databaseName, nextBackupType);

        result.IsHealthy().Should().BeTrue();
        result.Warnings.Should().BeEmpty();
        result.CriticalFindings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckStorageHealthAsync_WithBackupContext_ShouldNotWarn_WhenAlreadyUnhealthy()
    {
        var volumePath = "C:\\Backups";
        var databaseName = "TestDB";
        var nextBackupType = BackupType.Full;

        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 50L * 1024 * 1024 * 1024)); // Already critical (5%)

        var result = await _service.CheckStorageHealthAsync(volumePath, databaseName, nextBackupType);

        result.IsHealthy().Should().BeFalse();
        result.CriticalFindings.Should().ContainSingle();
        result.CriticalFindings.First().Should().Contain("Critically low storage space");
        // Should not call estimator when already unhealthy
        _backupSizeEstimatorMock.Verify(
            e => e.EstimateNextBackupSizeAsync(It.IsAny<string>(), It.IsAny<BackupType>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldReturnCritical_WhenVolumeUnavailable()
    {
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(false);

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeFalse();
        result.CriticalFindings.Should().ContainSingle();
        result.CriticalFindings.First().Should().Contain("Storage volume unavailable");
        result.TotalBytes.Should().Be(0);
        result.FreeBytes.Should().Be(0);
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldReturnCritical_WhenStorageInfoThrows()
    {
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ThrowsAsync(new InvalidOperationException("Drive not ready"));

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeFalse();
        result.CriticalFindings.Should().ContainSingle();
        result.CriticalFindings.First().Should().Contain("Failed to retrieve storage metrics");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckStorageHealthAsync_ShouldThrow_WhenVolumePathEmpty(string volumePath)
    {
        var act = async () => await _service.CheckStorageHealthAsync(volumePath);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Volume path cannot be empty*");
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldRespectExactThresholds()
    {
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((1000L * 1024 * 1024 * 1024, 200L * 1024 * 1024 * 1024)); // Exactly 20% free

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.IsHealthy().Should().BeFalse();
        result.Warnings.Should().ContainSingle();
        result.Warnings.First().Should().Contain("Low storage space");
    }

    [Fact]
    public async Task CheckStorageHealthAsync_ShouldFormatBytesReadably()
    {
        var volumePath = "C:\\Backups";
        _storageInfoProviderMock
            .Setup(p => p.IsVolumeAccessibleAsync(volumePath))
            .ReturnsAsync(true);
        _storageInfoProviderMock
            .Setup(p => p.GetStorageInfoAsync(volumePath))
            .ReturnsAsync((10_000L * 1024 * 1024 * 1024, 1_000L * 1024 * 1024 * 1024)); // 10 TB total, 1 TB free (10%)

        var result = await _service.CheckStorageHealthAsync(volumePath);

        result.CriticalFindings.First().Should().Contain("TB");
    }
}
