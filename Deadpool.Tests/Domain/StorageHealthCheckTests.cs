using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Domain;

public class StorageHealthCheckTests
{
    [Fact]
    public void Constructor_ShouldInitializeHealthyState()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.VolumePath.Should().Be("C:\\Backups");
        healthCheck.CheckTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        healthCheck.OverallHealth.Should().Be(HealthStatus.Healthy);
        healthCheck.Warnings.Should().BeEmpty();
        healthCheck.CriticalFindings.Should().BeEmpty();
        healthCheck.IsHealthy().Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenVolumePathEmpty(string volumePath)
    {
        var act = () => new StorageHealthCheck(volumePath);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Volume path cannot be empty*");
    }

    [Fact]
    public void RecordStorageMetrics_ShouldSetMetrics()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.RecordStorageMetrics(1000, 200);

        healthCheck.TotalBytes.Should().Be(1000);
        healthCheck.FreeBytes.Should().Be(200);
        healthCheck.FreePercentage.Should().Be(20m);
    }

    [Fact]
    public void RecordStorageMetrics_ShouldCalculatePercentage()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.RecordStorageMetrics(1000, 500);

        healthCheck.FreePercentage.Should().Be(50m);
    }

    [Fact]
    public void RecordStorageMetrics_ShouldReturnZeroPercentage_WhenTotalIsZero()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.RecordStorageMetrics(0, 0);

        healthCheck.FreePercentage.Should().Be(0m);
    }

    [Fact]
    public void RecordStorageMetrics_ShouldThrow_WhenTotalBytesNegative()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        var act = () => healthCheck.RecordStorageMetrics(-1, 100);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Total bytes cannot be negative*");
    }

    [Fact]
    public void RecordStorageMetrics_ShouldThrow_WhenFreeBytesNegative()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        var act = () => healthCheck.RecordStorageMetrics(1000, -1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Free bytes cannot be negative*");
    }

    [Fact]
    public void RecordStorageMetrics_ShouldThrow_WhenFreeBytesExceedsTotal()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        var act = () => healthCheck.RecordStorageMetrics(1000, 1001);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Free bytes cannot exceed total bytes*");
    }

    [Fact]
    public void AddWarning_ShouldChangeStateToWarning()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.AddWarning("Low storage space");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Warning);
        healthCheck.Warnings.Should().Contain("Low storage space");
        healthCheck.IsHealthy().Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddWarning_ShouldThrow_WhenMessageEmpty(string message)
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        var act = () => healthCheck.AddWarning(message);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Warning message cannot be empty*");
    }

    [Fact]
    public void AddCriticalFinding_ShouldChangeStateToCritical()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.AddCriticalFinding("Volume unavailable");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Critical);
        healthCheck.CriticalFindings.Should().Contain("Volume unavailable");
        healthCheck.IsHealthy().Should().BeFalse();
    }

    [Fact]
    public void AddCriticalFinding_ShouldOverrideWarningState()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.AddWarning("Low storage");
        healthCheck.AddCriticalFinding("Volume unavailable");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Critical);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddCriticalFinding_ShouldThrow_WhenMessageEmpty(string message)
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        var act = () => healthCheck.AddCriticalFinding(message);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Critical finding message cannot be empty*");
    }

    [Fact]
    public void AddWarning_ShouldNotDowngradeCriticalState()
    {
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        healthCheck.AddCriticalFinding("Volume unavailable");
        healthCheck.AddWarning("Low storage");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Critical);
    }
}
