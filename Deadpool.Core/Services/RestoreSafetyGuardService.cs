using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

/// <summary>
/// Enforces explicit restore confirmation for destructive database overwrite operations.
/// </summary>
public sealed class RestoreSafetyGuardService : IRestoreSafetyGuard
{
    public void EnsureConfirmed(RestoreConfirmationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.DatabaseName))
        {
            throw new InvalidOperationException("Restore blocked. Target database must be specified. This operation will overwrite database '<unknown>'.");
        }

        if (!context.Confirmed)
        {
            throw new InvalidOperationException(
                $"Restore not confirmed. This operation will overwrite database '{context.DatabaseName}'.");
        }

        if (context.RequireTextMatch)
        {
            // Exact case-sensitive match is required in strict mode.
            var matches = string.Equals(context.ConfirmationText, context.DatabaseName, StringComparison.Ordinal);
            if (!matches)
            {
                throw new InvalidOperationException(
                    $"Restore confirmation text mismatch. Type the exact database name '{context.DatabaseName}' to continue. This operation will overwrite database '{context.DatabaseName}'.");
            }
        }
    }
}
