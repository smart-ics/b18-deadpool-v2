using Deadpool.Core.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Domain;

public class RetentionPolicyTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var policy = new RetentionPolicy(
            fullBackupRetention: TimeSpan.FromDays(30),
            differentialBackupRetention: TimeSpan.FromDays(14),
            logBackupRetention: TimeSpan.FromDays(7));

        policy.FullBackupRetention.Should().Be(TimeSpan.FromDays(30));
        policy.DifferentialBackupRetention.Should().Be(TimeSpan.FromDays(14));
        policy.LogBackupRetention.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Default_ShouldProvideReasonableDefaults()
    {
        var policy = RetentionPolicy.Default;

        policy.FullBackupRetention.Should().Be(TimeSpan.FromDays(30));
        policy.DifferentialBackupRetention.Should().Be(TimeSpan.FromDays(14));
        policy.LogBackupRetention.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenFullRetentionZeroOrNegative()
    {
        var act = () => new RetentionPolicy(
            TimeSpan.Zero,
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(7));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Full backup retention must be positive*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDifferentialRetentionZeroOrNegative()
    {
        var act = () => new RetentionPolicy(
            TimeSpan.FromDays(30),
            TimeSpan.Zero,
            TimeSpan.FromDays(7));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Differential backup retention must be positive*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLogRetentionZeroOrNegative()
    {
        var act = () => new RetentionPolicy(
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(14),
            TimeSpan.Zero);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Log backup retention must be positive*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDifferentialRetentionExceedsFull()
    {
        var act = () => new RetentionPolicy(
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(45), // Longer than Full
            TimeSpan.FromDays(7));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Differential retention cannot exceed Full backup retention*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLogRetentionExceedsDifferential()
    {
        var act = () => new RetentionPolicy(
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(20)); // Longer than Differential

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Log retention cannot exceed Differential backup retention*");
    }

    [Fact]
    public void Constructor_ShouldAllow_ValidDescendingRetentionPeriods()
    {
        var act = () => new RetentionPolicy(
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(7));

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ShouldAllow_EqualRetentionPeriods()
    {
        var act = () => new RetentionPolicy(
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(30));

        act.Should().NotThrow();
    }
}
