using Deadpool.Core.Configuration;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Deadpool.Tests.Services;

public class RestoreExecutionServiceTests
{
    private readonly Mock<IRestoreScriptBuilderService> _scriptBuilderMock = new();
    private readonly RestoreExecutionService _service;

    public RestoreExecutionServiceTests()
    {
        _service = new RestoreExecutionService(
            _scriptBuilderMock.Object,
            Options.Create(new RestoreExecutionOptions { ConnectionString = string.Empty }),
            NullLogger<RestoreExecutionService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllowOverwriteIsFalse_Throws()
    {
        var plan = RestorePlan.CreateInvalidPlan("TestDB", DateTime.UtcNow, "Planner failed");

        Func<Task> act = async () => await _service.ExecuteAsync(plan, allowOverwrite: false);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*explicit overwrite consent*");

        _scriptBuilderMock.Verify(s => s.Build(It.IsAny<RestorePlan>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionStringMissing_ReturnsFailedResult()
    {
        var plan = RestorePlan.CreateInvalidPlan("TestDB", DateTime.UtcNow, "planner invalid on purpose");
        var script = new RestoreScript(new List<string> { "RESTORE DATABASE [TestDB] FROM DISK = 'C:\\temp\\full.bak' WITH RECOVERY;" });

        _scriptBuilderMock
            .Setup(s => s.Build(plan))
            .Returns(script);

        var result = await _service.ExecuteAsync(plan, allowOverwrite: true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.Steps.Should().BeEmpty();
    }
}
