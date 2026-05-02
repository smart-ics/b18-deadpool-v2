using Deadpool.Core.Configuration;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deadpool.Core.Services;

/// <summary>
/// Runtime restore orchestration entry point.
/// Enforces Planner -> Validator -> Executor flow.
/// </summary>
public sealed class RestoreOrchestratorService : IRestoreOrchestratorService
{
    private readonly IRestorePlannerService _planner;
    private readonly IRestorePlanValidatorService _validator;
    private readonly IRestoreExecutionService _executor;
    private readonly IOptions<RestoreOrchestratorOptions> _orchestratorOptions;
    private readonly ILogger<RestoreOrchestratorService> _logger;

    public RestoreOrchestratorService(
        IRestorePlannerService planner,
        IRestorePlanValidatorService validator,
        IRestoreExecutionService executor,
        IOptions<RestoreOrchestratorOptions> orchestratorOptions,
        ILogger<RestoreOrchestratorService> logger)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _orchestratorOptions = orchestratorOptions ?? throw new ArgumentNullException(nameof(orchestratorOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteRestore(DateTime targetTime, CancellationToken cancellationToken = default)
    {
        var databaseName = ResolveDatabaseName();

        var plan = await _planner.BuildRestorePlanAsync(databaseName, targetTime);

        var validation = _validator.Validate(plan);
        if (!validation.IsValid)
        {
            var message = "Restore validation failed: " + string.Join("; ", validation.Errors);
            _logger.LogError("Restore orchestration blocked by validation. Errors: {Errors}", validation.Errors);
            throw new InvalidOperationException(message);
        }

        var executionResult = await _executor.ExecuteAsync(
            plan,
            _orchestratorOptions.Value.AllowOverwrite,
            cancellationToken);

        if (!executionResult.Success)
        {
            var message = string.IsNullOrWhiteSpace(executionResult.ErrorMessage)
                ? "Restore execution failed."
                : executionResult.ErrorMessage;

            _logger.LogError(
                "Restore execution failed for {DatabaseName}. Steps executed: {StepCount}. Error: {Error}",
                plan.DatabaseName,
                executionResult.Steps.Count,
                message);

            throw new InvalidOperationException("Restore execution failed: " + message);
        }

        _logger.LogInformation(
            "Restore orchestration completed successfully for {DatabaseName}. Steps executed: {StepCount}.",
            plan.DatabaseName,
            executionResult.Steps.Count);
    }

    private string ResolveDatabaseName()
    {
        var databaseName = _orchestratorOptions.Value.DatabaseName;
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("RestoreOrchestrator:DatabaseName must be configured for restore orchestration.");

        return databaseName;
    }
}
