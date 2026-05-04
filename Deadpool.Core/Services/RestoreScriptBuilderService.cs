using System.Globalization;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadpool.Core.Services;

/// <summary>
/// Builds ordered SQL Server restore statements from a validated restore plan.
/// </summary>
public sealed class RestoreScriptBuilderService : IRestoreScriptBuilderService
{
    private readonly ILogger<RestoreScriptBuilderService> _logger;

    public RestoreScriptBuilderService(ILogger<RestoreScriptBuilderService>? logger = null)
    {
        _logger = logger ?? NullLogger<RestoreScriptBuilderService>.Instance;
    }

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
            "USE master;",
            BuildRestoreDatabaseWithNoRecovery(databaseName, plan.FullBackup.BackupFilePath, plan.AllowOverwrite)
        };

        if (plan.DifferentialBackup != null)
        {
            commands.Add(BuildRestoreDatabaseWithNoRecovery(databaseName, plan.DifferentialBackup.BackupFilePath, false));
        }

        if (plan.LogBackups.Any())
        {
            for (var i = 0; i < plan.LogBackups.Count - 1; i++)
            {
                commands.Add(BuildRestoreLogWithNoRecovery(databaseName, plan.LogBackups[i].BackupFilePath));
            }

            var lastLog = plan.LogBackups.Last();
            commands.Add(BuildFinalRestoreLog(databaseName, lastLog, plan.TargetTime));
        }
        else
        {
            commands.Add($"RESTORE DATABASE [{databaseName}] WITH RECOVERY;");
        }

        return new RestoreScript(commands);
    }

    private static string BuildRestoreDatabaseWithNoRecovery(string escapedDatabaseName, string path, bool allowOverwrite)
    {
        var command = $"RESTORE DATABASE [{escapedDatabaseName}] FROM DISK = '{EscapeStringLiteral(path)}' WITH NORECOVERY";
        if (allowOverwrite)
        {
            command += ", REPLACE";
        }
        return command + ";";
    }

    private static string BuildRestoreLogWithNoRecovery(string escapedDatabaseName, string path)
    {
        return $"RESTORE LOG [{escapedDatabaseName}] FROM DISK = '{EscapeStringLiteral(path)}' WITH NORECOVERY;";
    }

    private string BuildFinalRestoreLog(string escapedDatabaseName, BackupJob lastLog, DateTime targetTime)
    {
        var normalizedStopAt = NormalizeToSqlStopAt(targetTime);
        var normalizedStopAtLiteral = FormatStopAtLiteral(normalizedStopAt);

        if (!lastLog.EndTime.HasValue)
        {
            _logger.LogWarning(
                "Final restore fallback to RECOVERY without STOPAT because last log EndTime is missing. StopAt={StopAt} LastLogEndTime=<null> Comparison=unknown",
                normalizedStopAtLiteral);
            return BuildFinalRestoreLogWithRecoveryOnly(escapedDatabaseName, lastLog.BackupFilePath);
        }

        var normalizedLastLogEndTime = NormalizeToSqlStopAt(lastLog.EndTime.Value);
        var normalizedLastLogEndTimeLiteral = FormatStopAtLiteral(normalizedLastLogEndTime);
        var comparison = normalizedStopAt.CompareTo(normalizedLastLogEndTime);
        var clampedStopAt = comparison > 0 ? normalizedLastLogEndTime : normalizedStopAt;
        var clampedStopAtLiteral = FormatStopAtLiteral(clampedStopAt);

        _logger.LogInformation(
            "Final restore STOPAT evaluation. StopAt={StopAt} LastLogEndTime={LastLogEndTime} Comparison={Comparison} ClampedStopAt={ClampedStopAt}",
            normalizedStopAtLiteral,
            normalizedLastLogEndTimeLiteral,
            comparison,
            clampedStopAtLiteral);

        // At or beyond the final log boundary, STOPAT is not required and recovery-only is safer.
        if (clampedStopAt >= normalizedLastLogEndTime)
        {
            return BuildFinalRestoreLogWithRecoveryOnly(escapedDatabaseName, lastLog.BackupFilePath);
        }

        return $"RESTORE LOG [{escapedDatabaseName}] FROM DISK = '{EscapeStringLiteral(lastLog.BackupFilePath)}' WITH STOPAT = '{clampedStopAtLiteral}', RECOVERY;";
    }

    private static string BuildFinalRestoreLogWithRecoveryOnly(string escapedDatabaseName, string path)
    {
        return $"RESTORE LOG [{escapedDatabaseName}] FROM DISK = '{EscapeStringLiteral(path)}' WITH RECOVERY;";
    }

    private static DateTime NormalizeToSqlStopAt(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Unspecified);
    }

    private static string FormatStopAtLiteral(DateTime stopAt)
    {
        return stopAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
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
