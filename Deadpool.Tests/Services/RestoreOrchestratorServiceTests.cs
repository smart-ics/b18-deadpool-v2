using Deadpool.Core.Configuration;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Deadpool.Tests.Services;

public class RestoreOrchestratorServiceTests
{
    private readonly Mock<IRestorePlannerService> _plannerMock = new();
    private readonly Mock<IRestorePlanValidatorService> _validatorMock = new();
    private readonly Mock<IRestoreSafetyGuard> _safetyGuardMock = new();
    private readonly Mock<IRestoreExecutionService> _executorMock = new();

    [Fact]
    public async Task ExecuteRestore_WhenValidationFails_StopsBeforeExecutor()
    {
        var targetTime = DateTime.UtcNow;
        var plan = BuildValidPlan(targetTime);

        _plannerMock
            .Setup(p => p.BuildRestorePlanAsync("TestDB", targetTime))
            .ReturnsAsync(plan);

        var validation = new RestoreValidationResult();
        validation.AddError("Missing file");
        _validatorMock
            .Setup(v => v.Validate(plan))
            .Returns(validation);

        var sut = CreateSut();

        Func<Task> act = async () => await sut.ExecuteRestore(targetTime);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Restore validation failed:*Missing file*");

        validation.Errors.Should().NotBeEmpty();

        _executorMock.Verify(
            e => e.ExecuteAsync(It.IsAny<RestorePlan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _safetyGuardMock.Verify(
            s => s.EnsureConfirmed(It.IsAny<RestoreConfirmationContext>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteRestore_WhenValidationPasses_CallsExecutor()
    {
        var targetTime = DateTime.UtcNow;
        var plan = BuildValidPlan(targetTime);

        _plannerMock
            .Setup(p => p.BuildRestorePlanAsync("TestDB", targetTime))
            .ReturnsAsync(plan);

        _validatorMock
            .Setup(v => v.Validate(plan))
            .Returns(new RestoreValidationResult());

        _executorMock
            .Setup(e => e.ExecuteAsync(
                plan,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreExecutionResult { Success = true });

        var sut = CreateSut();

        await sut.ExecuteRestore(targetTime);

        _plannerMock.Verify(p => p.BuildRestorePlanAsync("TestDB", targetTime), Times.Once);
        _validatorMock.Verify(v => v.Validate(plan), Times.Once);
        _safetyGuardMock.Verify(
            s => s.EnsureConfirmed(It.Is<RestoreConfirmationContext>(c =>
                c.DatabaseName == "TestDB" &&
                c.Confirmed &&
                !c.RequireTextMatch)),
            Times.Once);
        _executorMock.Verify(
            e => e.ExecuteAsync(
                plan,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteRestore_WhenSafetyGuardBlocks_StopsBeforeExecutor()
    {
        var targetTime = DateTime.UtcNow;
        var plan = BuildValidPlan(targetTime);

        _plannerMock
            .Setup(p => p.BuildRestorePlanAsync("TestDB", targetTime))
            .ReturnsAsync(plan);

        _validatorMock
            .Setup(v => v.Validate(plan))
            .Returns(new RestoreValidationResult());

        _safetyGuardMock
            .Setup(s => s.EnsureConfirmed(It.IsAny<RestoreConfirmationContext>()))
            .Throws(new InvalidOperationException("Restore not confirmed. This operation will overwrite database 'TestDB'."));

        var sut = CreateSut();

        Func<Task> act = async () => await sut.ExecuteRestore(targetTime);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Restore not confirmed*overwrite database 'TestDB'*");

        _executorMock.Verify(
            e => e.ExecuteAsync(It.IsAny<RestorePlan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteRestore_WhenExecutorFails_Throws()
    {
        var targetTime = DateTime.UtcNow;
        var plan = BuildValidPlan(targetTime);

        _plannerMock
            .Setup(p => p.BuildRestorePlanAsync("TestDB", targetTime))
            .ReturnsAsync(plan);

        _validatorMock
            .Setup(v => v.Validate(plan))
            .Returns(new RestoreValidationResult());

        _executorMock
            .Setup(e => e.ExecuteAsync(plan, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreExecutionResult { Success = false, ErrorMessage = "SQL timeout" });

        var sut = CreateSut();

        Func<Task> act = async () => await sut.ExecuteRestore(targetTime);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Restore execution failed*SQL timeout*");
    }

    [Fact]
    public async Task ExecuteRestore_WhenNoDatabasePolicyConfigured_Throws()
    {
        var options = Options.Create(new RestoreOrchestratorOptions());

        var sut = new RestoreOrchestratorService(
            _plannerMock.Object,
            _validatorMock.Object,
            _safetyGuardMock.Object,
            _executorMock.Object,
            options,
            NullLogger<RestoreOrchestratorService>.Instance);

        Func<Task> act = async () => await sut.ExecuteRestore(DateTime.UtcNow);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RestoreOrchestrator:DatabaseName must be configured*");
    }

    private RestoreOrchestratorService CreateSut()
    {
        var options = new RestoreOrchestratorOptions
        {
            DatabaseName = "TestDB",
            AllowOverwrite = true,
            Confirmed = true,
            RequireTextMatch = false
        };

        return new RestoreOrchestratorService(
            _plannerMock.Object,
            _validatorMock.Object,
            _safetyGuardMock.Object,
            _executorMock.Object,
            Options.Create(options),
            NullLogger<RestoreOrchestratorService>.Instance);
    }

    private static RestorePlan BuildValidPlan(DateTime targetTime)
    {
        var fullStart = targetTime.AddHours(-2);
        var fullEnd = fullStart.AddMinutes(30);

        var full = BackupJob.Restore(
            "TestDB",
            BackupType.Full,
            BackupStatus.Completed,
            fullStart,
            fullEnd,
            @"C:\Backups\full.bak",
            100,
            null,
            1000,
            1100,
            null,
            1050);

        return RestorePlan.CreateValidPlan(
            "TestDB",
            targetTime,
            full,
            differentialBackup: null,
            logBackups: Array.Empty<BackupJob>(),
            actualRestorePoint: fullEnd);
    }
}
