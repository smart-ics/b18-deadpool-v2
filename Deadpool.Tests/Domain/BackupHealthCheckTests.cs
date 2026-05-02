using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Domain;

public class BackupHealthCheckTests
{
    [Fact]
    public void Constructor_ShouldInitializeHealthyState()
    {
        var healthCheck = new BackupHealthCheck("TestDB");

        healthCheck.DatabaseName.Should().Be("TestDB");
        healthCheck.CheckTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        healthCheck.OverallHealth.Should().Be(HealthStatus.Healthy);
        healthCheck.IsHealthy().Should().BeTrue();
        healthCheck.Warnings.Should().BeEmpty();
        healthCheck.CriticalFindings.Should().BeEmpty();
        healthCheck.Limitations.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDatabaseNameEmpty()
    {
        Action act = () => new BackupHealthCheck("");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Database name cannot be empty*");
    }

    [Fact]
    public void RecordLastSuccessfulFullBackup_ShouldSetTimestamp()
    {
        var healthCheck = new BackupHealthCheck("TestDB");
        var backupTime = DateTime.Now.AddHours(-2);

        healthCheck.RecordLastSuccessfulFullBackup(backupTime);

        healthCheck.LastSuccessfulFullBackup.Should().Be(backupTime);
    }

    [Fact]
    public void AddWarning_ShouldChangeStateToWarning()
    {
        var healthCheck = new BackupHealthCheck("TestDB");

        healthCheck.AddWarning("Backup overdue");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Warning);
        healthCheck.HasWarnings().Should().BeTrue();
        healthCheck.Warnings.Should().Contain("Backup overdue");
    }

    [Fact]
    public void AddCriticalFinding_ShouldChangeStateToCritical()
    {
        var healthCheck = new BackupHealthCheck("TestDB");

        healthCheck.AddCriticalFinding("No full backup found");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Critical);
        healthCheck.IsCritical().Should().BeTrue();
        healthCheck.CriticalFindings.Should().Contain("No full backup found");
    }

    [Fact]
    public void AddCriticalFinding_ShouldOverrideWarningState()
    {
        var healthCheck = new BackupHealthCheck("TestDB");
        healthCheck.AddWarning("Minor issue");

        healthCheck.AddCriticalFinding("Critical issue");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Critical);
        healthCheck.Warnings.Should().Contain("Minor issue");
        healthCheck.CriticalFindings.Should().Contain("Critical issue");
    }

    [Fact]
    public void AddWarning_ShouldNotDowngradeCriticalState()
    {
        var healthCheck = new BackupHealthCheck("TestDB");
        healthCheck.AddCriticalFinding("Critical issue");

        healthCheck.AddWarning("Warning");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Critical);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void AddWarning_ShouldThrow_WhenMessageEmpty(string message)
    {
        var healthCheck = new BackupHealthCheck("TestDB");

        Action act = () => healthCheck.AddWarning(message);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void AddCriticalFinding_ShouldThrow_WhenMessageEmpty(string message)
    {
        var healthCheck = new BackupHealthCheck("TestDB");

        Action act = () => healthCheck.AddCriticalFinding(message);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddLimitation_ShouldAddToLimitationsList()
    {
        var healthCheck = new BackupHealthCheck("TestDB");

        healthCheck.AddLimitation("LSN validation not implemented");

        healthCheck.Limitations.Should().Contain("LSN validation not implemented");
        healthCheck.OverallHealth.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void AddLimitation_ShouldNotAffectHealthStatus()
    {
        var healthCheck = new BackupHealthCheck("TestDB");
        healthCheck.AddWarning("Minor issue");

        healthCheck.AddLimitation("Some limitation");

        healthCheck.OverallHealth.Should().Be(HealthStatus.Warning);
        healthCheck.Limitations.Should().Contain("Some limitation");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void AddLimitation_ShouldThrow_WhenMessageEmpty(string message)
    {
        var healthCheck = new BackupHealthCheck("TestDB");

        Action act = () => healthCheck.AddLimitation(message);

        act.Should().Throw<ArgumentException>();
    }
}
