using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Core.Services;

/// <summary>
/// Guarded restore execution flow that enforces RestorePlan validation before execution.
/// </summary>
public sealed class RestoreExecutionService : IRestoreExecutionService
{
    private readonly IRestorePlanValidatorService _validator;
    private readonly ILogger<RestoreExecutionService> _logger;

    public RestoreExecutionService(
        IRestorePlanValidatorService validator,
        ILogger<RestoreExecutionService> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(
        RestorePlan plan,
        Func<RestorePlan, CancellationToken, Task> executeRestoreAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(executeRestoreAsync);

        var validation = _validator.Validate(plan);
        if (!validation.IsValid)
        {
            var message = "Restore validation failed: " + string.Join("; ", validation.Errors);
            _logger.LogError("Restore execution blocked by validation. Errors: {Errors}", validation.Errors);
            throw new InvalidOperationException(message);
        }

        await executeRestoreAsync(plan, cancellationToken);
    }
}
