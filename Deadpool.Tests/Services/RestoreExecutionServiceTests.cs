using Deadpool.Core.Configuration;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Deadpool.Tests.Services;

public class RestoreExecutionServiceTests
{
    private readonly Mock<IRestoreScriptBuilderService> _scriptBuilderMock = new();
    private readonly TestableRestoreExecutionService _service;
    private readonly RestoreExecutionService _serviceWithMissingConnectionString;

    public RestoreExecutionServiceTests()
    {
        _service = new TestableRestoreExecutionService(
            _scriptBuilderMock.Object,
            Options.Create(new RestoreExecutionOptions { ConnectionString = "Server=placeholder;Database=master;" }),
            NullLogger<RestoreExecutionService>.Instance);

        _serviceWithMissingConnectionString = new RestoreExecutionService(
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

        var result = await _serviceWithMissingConnectionString.ExecuteAsync(plan, allowOverwrite: true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FirstStepFails_StopsImmediately_AndLogsOnlyFirstStep()
    {
        var plan = BuildPlan();
        var commands = new[]
        {
            "RESTORE DATABASE [TestDB] FROM DISK = 'a' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'b' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'c' WITH RECOVERY;"
        };

        _scriptBuilderMock
            .Setup(s => s.Build(plan))
            .Returns(new RestoreScript(commands));

        _service.Configure(commands, failAtStep: 1);

        var result = await _service.ExecuteAsync(plan, allowOverwrite: true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _service.ExecutedCommands.Should().HaveCount(1);
        _service.ExecutedCommands[0].Should().Be(commands[0]);

        result.Steps.Should().HaveCount(1);
        result.Steps[0].Command.Should().Be(commands[0]);
        result.Steps[0].Success.Should().BeFalse();
        result.Steps[0].Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_MidChainFailure_StopsAndDoesNotExecuteLaterSteps()
    {
        var plan = BuildPlan();
        var commands = new[]
        {
            "RESTORE DATABASE [TestDB] FROM DISK = 'a' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'b' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'c' WITH RECOVERY;"
        };

        _scriptBuilderMock
            .Setup(s => s.Build(plan))
            .Returns(new RestoreScript(commands));

        _service.Configure(commands, failAtStep: 2);

        var result = await _service.ExecuteAsync(plan, allowOverwrite: true);

        result.Success.Should().BeFalse();
        _service.ExecutedCommands.Should().HaveCount(2);
        _service.ExecutedCommands[0].Should().Be(commands[0]);
        _service.ExecutedCommands[1].Should().Be(commands[1]);

        result.Steps.Should().HaveCount(2);
        result.Steps[0].Command.Should().Be(commands[0]);
        result.Steps[0].Success.Should().BeTrue();
        result.Steps[1].Command.Should().Be(commands[1]);
        result.Steps[1].Success.Should().BeFalse();
        result.Steps[1].Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_LastStepFailure_PriorStepsSucceed_ThenFails()
    {
        var plan = BuildPlan();
        var commands = new[]
        {
            "RESTORE DATABASE [TestDB] FROM DISK = 'a' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'b' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'c' WITH RECOVERY;"
        };

        _scriptBuilderMock
            .Setup(s => s.Build(plan))
            .Returns(new RestoreScript(commands));

        _service.Configure(commands, failAtStep: 3);

        var result = await _service.ExecuteAsync(plan, allowOverwrite: true);

        result.Success.Should().BeFalse();
        _service.ExecutedCommands.Should().HaveCount(3);
        result.Steps.Should().HaveCount(3);

        result.Steps[0].Success.Should().BeTrue();
        result.Steps[1].Success.Should().BeTrue();
        result.Steps[2].Success.Should().BeFalse();
        result.Steps[2].Error.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoFailures_ProducesOrderedSuccessfulStepLogs()
    {
        var plan = BuildPlan();
        var commands = new[]
        {
            "RESTORE DATABASE [TestDB] FROM DISK = 'a' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'b' WITH NORECOVERY;",
            "RESTORE LOG [TestDB] FROM DISK = 'c' WITH RECOVERY;"
        };

        _scriptBuilderMock
            .Setup(s => s.Build(plan))
            .Returns(new RestoreScript(commands));

        _service.Configure(commands, failAtStep: null);

        var result = await _service.ExecuteAsync(plan, allowOverwrite: true);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _service.ExecutedCommands.Should().HaveCount(3);
        result.Steps.Should().HaveCount(3);
        result.Steps.Select(s => s.Command).Should().Equal(commands);
        result.Steps.Should().OnlyContain(s => s.Success && s.Error == null);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSqlTimeoutOccurs_MapsErrorMessageAsSqlTimeout()
    {
        var plan = BuildPlan();
        var commands = new[]
        {
            "RESTORE DATABASE [TestDB] FROM DISK = 'a' WITH NORECOVERY;"
        };

        _scriptBuilderMock
            .Setup(s => s.Build(plan))
            .Returns(new RestoreScript(commands));

        _service.Configure(commands, failAtStep: 1, throwSqlTimeout: true);

        var result = await _service.ExecuteAsync(plan, allowOverwrite: true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("SQL Timeout: ");
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Error.Should().StartWith("SQL Timeout: ");
    }

    private static RestorePlan BuildPlan()
    {
        return RestorePlan.CreateInvalidPlan("TestDB", DateTime.UtcNow, "test plan");
    }

    private sealed class TestableRestoreExecutionService : RestoreExecutionService
    {
        private readonly Queue<string> _orderedCommands = new();
        private int? _failAtStep;
        private bool _throwSqlTimeout;
        private int _currentStep;

        public List<string> ExecutedCommands { get; } = new();

        public TestableRestoreExecutionService(
            IRestoreScriptBuilderService scriptBuilder,
            IOptions<RestoreExecutionOptions> options,
            Microsoft.Extensions.Logging.ILogger<RestoreExecutionService> logger)
            : base(scriptBuilder, options, logger)
        {
        }

        public void Configure(IEnumerable<string> commands, int? failAtStep, bool throwSqlTimeout = false)
        {
            _orderedCommands.Clear();
            foreach (var command in commands)
            {
                _orderedCommands.Enqueue(command);
            }

            _failAtStep = failAtStep;
            _throwSqlTimeout = throwSqlTimeout;
            _currentStep = 0;
            ExecutedCommands.Clear();
        }

        protected override Task<bool> DatabaseExistsAsync(SqlConnection connection, string databaseName, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override Task OpenConnectionAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected override Task ExecuteCommandAsync(SqlConnection connection, string commandText, CancellationToken cancellationToken)
        {
            _currentStep++;
            ExecutedCommands.Add(commandText);

            if (_orderedCommands.Count > 0)
            {
                var expected = _orderedCommands.Dequeue();
                expected.Should().Be(commandText);
            }

            if (_failAtStep.HasValue && _currentStep == _failAtStep.Value)
            {
                if (_throwSqlTimeout)
                {
                    throw CreateSqlException(-2, "Execution timeout expired.");
                }

                throw new InvalidOperationException("Simulated step failure.");
            }

            return Task.CompletedTask;
        }

        private static SqlException CreateSqlException(int number, string message)
        {
            var errorCollection = Construct<SqlErrorCollection>();
            var error = CreateSqlError(number, message);

            var addMethod = typeof(SqlErrorCollection)
                .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            addMethod!.Invoke(errorCollection, new object[] { error });

            var sqlExceptionFactory = typeof(SqlException)
                .GetMethod(
                    "CreateException",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(SqlErrorCollection), typeof(string) },
                    modifiers: null);

            return (SqlException)sqlExceptionFactory!.Invoke(null, new object[] { errorCollection, "11.0.0" })!;
        }

        private static SqlError CreateSqlError(int number, string message)
        {
            var ctor = typeof(SqlError)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            var args = ctor.GetParameters()
                .Select(p => GetDefaultValue(p.ParameterType, number, message))
                .ToArray();

            return (SqlError)ctor.Invoke(args);
        }

        private static T Construct<T>() where T : class
        {
            var ctor = typeof(T)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(c => c.GetParameters().Length == 0);
            return (T)ctor.Invoke(Array.Empty<object>());
        }

        private static object? GetDefaultValue(Type parameterType, int number, string message)
        {
            if (parameterType == typeof(int)) return number;
            if (parameterType == typeof(string)) return message;
            if (parameterType == typeof(byte)) return (byte)0;
            if (parameterType == typeof(uint)) return 0u;
            if (parameterType == typeof(Exception)) return null;
            if (parameterType == typeof(bool)) return false;
            if (parameterType == typeof(Guid)) return Guid.Empty;

            return parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
        }
    }
}
