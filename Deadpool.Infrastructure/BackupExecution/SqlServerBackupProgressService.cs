using Dapper;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Data.SqlClient;

namespace Deadpool.Infrastructure.BackupExecution;

public sealed class SqlServerBackupProgressService : IBackupProgressService
{
    private readonly string _connectionString;

    public SqlServerBackupProgressService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<BackupProgress?> GetRunningBackupAsync()
    {
        const string query = """
            SELECT TOP (1)
                r.session_id,
                r.command,
                r.percent_complete,
                r.start_time,
                r.total_elapsed_time / 1000 AS elapsed_seconds,
                r.estimated_completion_time / 1000 AS remaining_seconds,
                st.text AS command_text
            FROM sys.dm_exec_requests r
            OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) st
            WHERE r.command IN ('BACKUP DATABASE', 'BACKUP LOG')
            ORDER BY r.start_time DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<BackupProgressRow>(query);
        if (row == null)
        {
            return null;
        }

        return new BackupProgress
        {
            BackupType = MapBackupType(row.Command, row.CommandText),
            PercentComplete = Math.Clamp(row.PercentComplete, 0d, 100d),
            StartTime = row.StartTime,
            ElapsedSeconds = Math.Max(0, row.ElapsedSeconds),
            RemainingSeconds = Math.Max(0, row.RemainingSeconds)
        };
    }

    private static string MapBackupType(string command, string? commandText)
    {
        if (command.Equals("BACKUP LOG", StringComparison.OrdinalIgnoreCase))
        {
            return "LOG";
        }

        if (!string.IsNullOrWhiteSpace(commandText) &&
            commandText.Contains("DIFFERENTIAL", StringComparison.OrdinalIgnoreCase))
        {
            return "DIFF";
        }

        return "FULL";
    }

    private sealed class BackupProgressRow
    {
        public string Command { get; init; } = string.Empty;
        public double PercentComplete { get; init; }
        public DateTime StartTime { get; init; }
        public int ElapsedSeconds { get; init; }
        public int RemainingSeconds { get; init; }
        public string? CommandText { get; init; }
    }
}
