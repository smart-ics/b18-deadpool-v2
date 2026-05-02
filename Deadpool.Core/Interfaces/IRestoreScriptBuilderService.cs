using Deadpool.Core.Domain.ValueObjects;

namespace Deadpool.Core.Interfaces;

/// <summary>
/// Builds SQL Server restore scripts from a restore plan.
/// </summary>
public interface IRestoreScriptBuilderService
{
    RestoreScript Build(RestorePlan plan);
}
