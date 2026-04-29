using Deadpool.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Deadpool.Tests.Integration;

public class FileSystemStorageInfoProviderTests
{
    private readonly FileSystemStorageInfoProvider _provider;

    public FileSystemStorageInfoProviderTests()
    {
        _provider = new FileSystemStorageInfoProvider(
            NullLogger<FileSystemStorageInfoProvider>.Instance);
    }

    [Fact]
    public async Task GetStorageInfoAsync_ShouldReturnValidInfo_ForCurrentDrive()
    {
        var currentPath = Directory.GetCurrentDirectory();

        var (totalBytes, freeBytes) = await _provider.GetStorageInfoAsync(currentPath);

        totalBytes.Should().BeGreaterThan(0);
        freeBytes.Should().BeGreaterThan(0);
        freeBytes.Should().BeLessThanOrEqualTo(totalBytes);
    }

    [Fact]
    public async Task IsVolumeAccessibleAsync_ShouldReturnTrue_ForCurrentDrive()
    {
        var currentPath = Directory.GetCurrentDirectory();

        var isAccessible = await _provider.IsVolumeAccessibleAsync(currentPath);

        isAccessible.Should().BeTrue();
    }

    [Fact]
    public async Task IsVolumeAccessibleAsync_ShouldReturnFalse_ForInvalidDrive()
    {
        var invalidPath = "Z:\\NonExistentDrive";

        var isAccessible = await _provider.IsVolumeAccessibleAsync(invalidPath);

        isAccessible.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStorageInfoAsync_ShouldThrow_WhenVolumePathEmpty(string volumePath)
    {
        var act = async () => await _provider.GetStorageInfoAsync(volumePath);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Volume path cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsVolumeAccessibleAsync_ShouldThrow_WhenVolumePathEmpty(string volumePath)
    {
        var act = async () => await _provider.IsVolumeAccessibleAsync(volumePath);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Volume path cannot be empty*");
    }
}
