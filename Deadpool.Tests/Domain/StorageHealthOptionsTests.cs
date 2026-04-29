using Deadpool.Core.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Domain;

public class StorageHealthOptionsTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var options = new StorageHealthOptions(
            warningThresholdPercentage: 25,
            criticalThresholdPercentage: 15,
            minimumWarningFreeSpaceBytes: 100L * 1024 * 1024 * 1024,
            minimumCriticalFreeSpaceBytes: 50L * 1024 * 1024 * 1024);

        options.WarningThresholdPercentage.Should().Be(25);
        options.CriticalThresholdPercentage.Should().Be(15);
        options.MinimumWarningFreeSpaceBytes.Should().Be(100L * 1024 * 1024 * 1024);
        options.MinimumCriticalFreeSpaceBytes.Should().Be(50L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void Default_ShouldProvideReasonableDefaults()
    {
        var options = StorageHealthOptions.Default;

        options.WarningThresholdPercentage.Should().Be(20);
        options.CriticalThresholdPercentage.Should().Be(10);
        options.MinimumWarningFreeSpaceBytes.Should().Be(50L * 1024 * 1024 * 1024); // 50 GB
        options.MinimumCriticalFreeSpaceBytes.Should().Be(20L * 1024 * 1024 * 1024); // 20 GB
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_ShouldThrow_WhenWarningThresholdInvalid(decimal warningThreshold)
    {
        var act = () => new StorageHealthOptions(warningThreshold, 10, 100, 50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Warning threshold must be between 0 and 100*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_ShouldThrow_WhenCriticalThresholdInvalid(decimal criticalThreshold)
    {
        var act = () => new StorageHealthOptions(20, criticalThreshold, 100, 50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Critical threshold must be between 0 and 100*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCriticalNotLowerThanWarning()
    {
        var act = () => new StorageHealthOptions(10, 20, 100, 50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Critical threshold must be lower than warning threshold*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCriticalEqualsWarning()
    {
        var act = () => new StorageHealthOptions(15, 15, 100, 50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Critical threshold must be lower than warning threshold*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMinimumWarningFreeSpaceNegative()
    {
        var act = () => new StorageHealthOptions(20, 10, -1, 50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Minimum warning free space cannot be negative*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMinimumCriticalFreeSpaceNegative()
    {
        var act = () => new StorageHealthOptions(20, 10, 100, -1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Minimum critical free space cannot be negative*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCriticalAbsoluteNotLowerThanWarning()
    {
        var act = () => new StorageHealthOptions(20, 10, 50, 100);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Critical absolute threshold must be lower than warning absolute threshold*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCriticalAbsoluteEqualsWarning()
    {
        var act = () => new StorageHealthOptions(20, 10, 100, 100);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Critical absolute threshold must be lower than warning absolute threshold*");
    }
}
