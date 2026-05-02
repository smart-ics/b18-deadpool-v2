using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Validates whether a restore plan is executable before restore execution.
/// </summary>
public interface IRestorePlanValidatorService
{
    /// <summary>
    /// Performs deterministic physical and defensive chain validation for a restore plan.
    /// </summary>
    RestoreValidationResult Validate(RestorePlan plan);
}
