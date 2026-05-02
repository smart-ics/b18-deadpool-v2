using Deadpool.Core.Configuration;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.ValueObjects;
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
    private readonly IRestoreSafetyGuard _safetyGuard;
    private readonly IRestoreExecutionService _executor;
    private readonly IRestoreHistoryRepository _historyRepository;
    private readonly IOptions<RestoreOrchestratorOptions> _orchestratorOptions;
    private readonly ILogger<RestoreOrchestratorService> _logger;

    public RestoreOrchestratorService(
        IRestorePlannerService planner,
        IRestorePlanValidatorService validator,
        IRestoreSafetyGuard safetyGuard,
        IRestoreExecutionService executor,
        IRestoreHistoryRepository historyRepository,
        IOptions<RestoreOrchestratorOptions> orchestratorOptions,
        ILogger<RestoreOrchestratorService> logger)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _orchestratorOptions = orchestratorOptions ?? throw new ArgumentNullException(nameof(orchestratorOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteRestore(DateTime targetTime, CancellationToken cancellationToken = default)
    {
        var confirmation = new RestoreConfirmationContext
        {
            DatabaseName = _orchestratorOptions.Value.DatabaseName,
            Confirmed = _orchestratorOptions.Value.Confirmed,
            ConfirmationText = _orchestratorOptions.Value.ConfirmationText,
            RequireTextMatch = _orchestratorOptions.Value.RequireTextMatch
        };

        await ExecuteRestore(targetTime, confirmation, cancellationToken);
    }

    public async Task ExecuteRestore(
        DateTime targetTime,
        RestoreConfirmationContext confirmationContext,
        CancellationToken cancellationToken = default)
    {
        var databaseName = ResolveDatabaseName();

        var executionStart = DateTime.Now;
        RestorePlan? plan = null;
        var executionResult = new RestoreExecutionResult
        {
            Success = false
        };

        try
        {
            plan = await _planner.BuildRestorePlanAsync(databaseName, targetTime);

            var validation = _validator.Validate(plan);
            if (!validation.IsValid)
            {
                var message = "Restore validation failed: " + string.Join("; ", validation.Errors);
                _logger.LogError("Restore orchestration blocked by validation. Errors: {Errors}", validation.Errors);
                throw new InvalidOperationException(message);
            }

            var effectiveConfirmation = new RestoreConfirmationContext
            {
                DatabaseName = plan.DatabaseName,
                Confirmed = confirmationContext.Confirmed,
                ConfirmationText = confirmationContext.ConfirmationText,
                RequireTextMatch = confirmationContext.RequireTextMatch
            };

            _safetyGuard.EnsureConfirmed(effectiveConfirmation);

            executionResult = await _executor.ExecuteAsync(
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
        catch (Exception ex)
        {
            executionResult.Success = false;
            executionResult.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            await TrySaveRestoreHistoryAsync(plan, databaseName, targetTime, executionStart, executionResult);
        }
    }

    private async Task TrySaveRestoreHistoryAsync(
        RestorePlan? plan,
        string fallbackDatabaseName,
        DateTime targetTime,
        DateTime executionStart,
        RestoreExecutionResult executionResult)
    {
        try
        {
            var duration = DateTime.Now - executionStart;
            var durationMs = Math.Max(0L, (long)duration.TotalMilliseconds);

            var record = new RestoreHistoryRecord
            {
                DatabaseName = plan?.DatabaseName ?? fallbackDatabaseName,
                RestoreTimestamp = executionStart,
                TargetRestoreTime = plan?.TargetTime ?? targetTime,
                FullBackupFile = plan?.FullBackup?.BackupFilePath ?? string.Empty,
                DiffBackupFile = plan?.DifferentialBackup?.BackupFilePath,
                LogBackupFiles = plan?.LogBackups.Select(x => x.BackupFilePath).ToList() ?? new List<string>(),
                Success = executionResult.Success,
                DurationMs = durationMs,
                ErrorMessage = executionResult.ErrorMessage
            };

            await _historyRepository.SaveAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist restore history for {DatabaseName}. Restore execution result remains unchanged.",
                plan?.DatabaseName ?? fallbackDatabaseName);
        }
    }

    private string ResolveDatabaseName()
    {
        var databaseName = _orchestratorOptions.Value.DatabaseName;
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("RestoreOrchestrator:DatabaseName must be configured for restore orchestration.");

        return databaseName;
    }
}
