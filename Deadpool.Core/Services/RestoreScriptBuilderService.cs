using System.Globalization;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.Core.Services;

/// <summary>
/// Builds ordered SQL Server restore statements from a validated restore plan.
/// </summary>
public sealed class RestoreScriptBuilderService : IRestoreScriptBuilderService
{
    public RestoreScript Build(RestorePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.IsValid)
            throw new ArgumentException("Restore plan must be valid to build a restore script.", nameof(plan));

        if (plan.FullBackup == null)
            throw new ArgumentException("Restore plan must include a Full backup.", nameof(plan));

        var databaseName = EscapeIdentifier(plan.DatabaseName);
        var commands = new List<string>
        {
            BuildRestoreDatabaseWithNoRecovery(databaseName, plan.FullBackup.BackupFilePath)
        };

        if (plan.DifferentialBackup != null)
        {
            commands.Add(BuildRestoreDatabaseWithNoRecovery(databaseName, plan.DifferentialBackup.BackupFilePath));
        }

        if (plan.LogBackups.Any())
        {
            for (var i = 0; i < plan.LogBackups.Count - 1; i++)
            {
                commands.Add(BuildRestoreLogWithNoRecovery(databaseName, plan.LogBackups[i].BackupFilePath));
            }

            var lastLogPath = plan.LogBackups.Last().BackupFilePath;
            commands.Add(BuildFinalRestoreLogWithStopAt(databaseName, lastLogPath, plan.TargetTime));
        }
        else
        {
            commands.Add($"RESTORE DATABASE [{databaseName}] WITH RECOVERY;");
        }

        return new RestoreScript(commands);
    }

    private static string BuildRestoreDatabaseWithNoRecovery(string escapedDatabaseName, string path)
    {
        return $"RESTORE DATABASE [{escapedDatabaseName}] FROM DISK = '{EscapeStringLiteral(path)}' WITH NORECOVERY;";
    }

    private static string BuildRestoreLogWithNoRecovery(string escapedDatabaseName, string path)
    {
        return $"RESTORE LOG [{escapedDatabaseName}] FROM DISK = '{EscapeStringLiteral(path)}' WITH NORECOVERY;";
    }

    private static string BuildFinalRestoreLogWithStopAt(string escapedDatabaseName, string path, DateTime stopAt)
    {
        var stopAtLiteral = FormatStopAtLiteral(stopAt);
        return $"RESTORE LOG [{escapedDatabaseName}] FROM DISK = '{EscapeStringLiteral(path)}' WITH STOPAT = '{stopAtLiteral}', RECOVERY;";
    }

    private static string FormatStopAtLiteral(DateTime stopAt)
    {
        // Preserve sub-second precision and include explicit offset context for deterministic point-in-time restore.
        var normalized = stopAt.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(stopAt, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(stopAt),
            _ => new DateTimeOffset(DateTime.SpecifyKind(stopAt, DateTimeKind.Utc), TimeSpan.Zero)
        };

        return normalized.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK", CultureInfo.InvariantCulture);
    }

    private static string EscapeIdentifier(string value)
    {
        return (value ?? string.Empty).Replace("]", "]]");
    }

    private static string EscapeStringLiteral(string value)
    {
        return (value ?? string.Empty).Replace("'", "''");
    }
}
