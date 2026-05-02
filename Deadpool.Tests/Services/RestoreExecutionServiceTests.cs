using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Deadpool.Tests.Services;

public class RestoreExecutionServiceTests
{
    private readonly Mock<IRestorePlanValidatorService> _validatorMock = new();
    private readonly RestoreExecutionService _service;

    public RestoreExecutionServiceTests()
    {
        _service = new RestoreExecutionService(_validatorMock.Object, NullLogger<RestoreExecutionService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ValidationFails_ThrowsAndDoesNotExecuteRestore()
    {
        var plan = RestorePlan.CreateInvalidPlan("TestDB", DateTime.UtcNow, "Planner failed");

        _validatorMock
            .Setup(v => v.Validate(plan))
            .Returns(new RestoreValidationResult
            {
            });

        // Force an explicit validation error.
        var result = new RestoreValidationResult();
        result.AddError("Missing file");
        _validatorMock
            .Setup(v => v.Validate(plan))
            .Returns(result);

        var executed = false;

        Func<Task> act = async () => await _service.ExecuteAsync(
            plan,
            (_, _) =>
            {
                executed = true;
                return Task.CompletedTask;
            });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Restore validation failed:*Missing file*");

        executed.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ValidationPasses_ExecutesRestoreDelegate()
    {
        var plan = RestorePlan.CreateInvalidPlan("TestDB", DateTime.UtcNow, "planner invalid on purpose");
        var success = new RestoreValidationResult();

        _validatorMock
            .Setup(v => v.Validate(plan))
            .Returns(success);

        var executed = false;

        await _service.ExecuteAsync(
            plan,
            (_, _) =>
            {
                executed = true;
                return Task.CompletedTask;
            });

        executed.Should().BeTrue();
    }
}
