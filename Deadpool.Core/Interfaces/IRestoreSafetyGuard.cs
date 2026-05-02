using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Blocks restore execution unless explicit operator confirmation requirements are satisfied.
/// </summary>
public interface IRestoreSafetyGuard
{
    void EnsureConfirmed(RestoreConfirmationContext context);
}
