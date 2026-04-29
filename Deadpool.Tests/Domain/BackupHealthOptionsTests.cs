using Deadpool.Core.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Domain;

public class BackupHealthOptionsTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var options = new BackupHealthOptions(
            fullBackupOverdueThreshold: TimeSpan.FromHours(24),
            differentialBackupOverdueThreshold: TimeSpan.FromHours(6),
            logBackupOverdueThreshold: TimeSpan.FromMinutes(30),
            chainLookbackPeriod: TimeSpan.FromDays(7)
        );

        options.FullBackupOverdueThreshold.Should().Be(TimeSpan.FromHours(24));
        options.DifferentialBackupOverdueThreshold.Should().Be(TimeSpan.FromHours(6));
        options.LogBackupOverdueThreshold.Should().Be(TimeSpan.FromMinutes(30));
        options.ChainLookbackPeriod.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenFullBackupThresholdInvalid()
    {
        Action act = () => new BackupHealthOptions(
            fullBackupOverdueThreshold: TimeSpan.Zero,
            differentialBackupOverdueThreshold: TimeSpan.FromHours(6),
            logBackupOverdueThreshold: TimeSpan.FromMinutes(30),
            chainLookbackPeriod: TimeSpan.FromDays(7)
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Full backup overdue threshold must be positive*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDifferentialBackupThresholdInvalid()
    {
        Action act = () => new BackupHealthOptions(
            fullBackupOverdueThreshold: TimeSpan.FromHours(24),
            differentialBackupOverdueThreshold: TimeSpan.Zero,
            logBackupOverdueThreshold: TimeSpan.FromMinutes(30),
            chainLookbackPeriod: TimeSpan.FromDays(7)
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Differential backup overdue threshold must be positive*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLogBackupThresholdInvalid()
    {
        Action act = () => new BackupHealthOptions(
            fullBackupOverdueThreshold: TimeSpan.FromHours(24),
            differentialBackupOverdueThreshold: TimeSpan.FromHours(6),
            logBackupOverdueThreshold: TimeSpan.Zero,
            chainLookbackPeriod: TimeSpan.FromDays(7)
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Log backup overdue threshold must be positive*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenChainLookbackInvalid()
    {
        Action act = () => new BackupHealthOptions(
            fullBackupOverdueThreshold: TimeSpan.FromHours(24),
            differentialBackupOverdueThreshold: TimeSpan.FromHours(6),
            logBackupOverdueThreshold: TimeSpan.FromMinutes(30),
            chainLookbackPeriod: TimeSpan.Zero
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Chain lookback period must be positive*");
    }

    [Fact]
    public void Default_ShouldProvideReasonableDefaults()
    {
        var options = BackupHealthOptions.Default;

        options.FullBackupOverdueThreshold.Should().Be(TimeSpan.FromHours(26));
        options.DifferentialBackupOverdueThreshold.Should().Be(TimeSpan.FromHours(6));
        options.LogBackupOverdueThreshold.Should().Be(TimeSpan.FromMinutes(30));
        options.ChainLookbackPeriod.Should().Be(TimeSpan.FromDays(7));
    }
}
