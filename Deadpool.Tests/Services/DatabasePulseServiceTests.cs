using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Deadpool.Tests.Services;

public class DatabasePulseServiceTests
{
    [Fact]
    public async Task CheckAsync_ShouldReturnHealthy_WhenProbeSucceeds()
    {
        // Arrange
        var probeMock = new Mock<IDatabaseConnectivityProbe>();
        probeMock.Setup(x => x.ProbeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DatabasePulseService(probeMock.Object, NullLogger<DatabasePulseService>.Instance);

        // Act
        var result = await service.CheckAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnCritical_WhenProbeThrows()
    {
        // Arrange
        var probeMock = new Mock<IDatabaseConnectivityProbe>();
        probeMock.Setup(x => x.ProbeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var service = new DatabasePulseService(probeMock.Object, NullLogger<DatabasePulseService>.Instance);

        // Act
        var result = await service.CheckAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Critical);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnCritical_WhenProbeFailsOnQuery()
    {
        // Arrange
        var probeMock = new Mock<IDatabaseConnectivityProbe>();
        probeMock.Setup(x => x.ProbeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SELECT 1 failed"));

        var service = new DatabasePulseService(probeMock.Object, NullLogger<DatabasePulseService>.Instance);

        // Act
        var result = await service.CheckAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Critical);
        result.ErrorMessage.Should().Contain("SELECT 1 failed");
    }
}
