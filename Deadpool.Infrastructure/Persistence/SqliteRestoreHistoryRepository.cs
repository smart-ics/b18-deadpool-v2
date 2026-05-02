using System.Globalization;
using System.Text.Json;
using Dapper;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence;

public sealed class SqliteRestoreHistoryRepository : IRestoreHistoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteRestoreHistoryRepository> _logger;

    public SqliteRestoreHistoryRepository(string databasePath, ILogger<SqliteRestoreHistoryRepository> logger)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be empty.", nameof(databasePath));

        _connectionString = $"Data Source={databasePath};";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        EnsureDatabaseCreated();
    }

    private void EnsureDatabaseCreated()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            CREATE TABLE IF NOT EXISTS RestoreHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DatabaseName TEXT NOT NULL,
                RestoreTimestamp TEXT NOT NULL,
                TargetRestoreTime TEXT NOT NULL,
                FullBackupFile TEXT NOT NULL,
                DiffBackupFile TEXT,
                LogBackupFilesJson TEXT NOT NULL,
                Success INTEGER NOT NULL,
                DurationMs INTEGER NOT NULL,
                ErrorMessage TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_restorehistory_timestamp
            ON RestoreHistory(RestoreTimestamp DESC);
        ";

        connection.Execute(sql);
        _logger.LogInformation("SQLite restore history repository initialized at {ConnectionString}", _connectionString);
    }

    public async Task SaveAsync(RestoreHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO RestoreHistory (
                DatabaseName,
                RestoreTimestamp,
                TargetRestoreTime,
                FullBackupFile,
                DiffBackupFile,
                LogBackupFilesJson,
                Success,
                DurationMs,
                ErrorMessage
            ) VALUES (
                @DatabaseName,
                @RestoreTimestamp,
                @TargetRestoreTime,
                @FullBackupFile,
                @DiffBackupFile,
                @LogBackupFilesJson,
                @Success,
                @DurationMs,
                @ErrorMessage
            );
        ";

        await connection.ExecuteAsync(sql, new
        {
            record.DatabaseName,
            RestoreTimestamp = record.RestoreTimestamp.ToString("O"),
            TargetRestoreTime = record.TargetRestoreTime.ToString("O"),
            record.FullBackupFile,
            record.DiffBackupFile,
            LogBackupFilesJson = JsonSerializer.Serialize(record.LogBackupFiles),
            Success = record.Success ? 1 : 0,
            record.DurationMs,
            record.ErrorMessage
        });
    }

    public async Task<IReadOnlyList<RestoreHistoryRecord>> GetRecentAsync(int limit)
    {
        if (limit <= 0)
            return Array.Empty<RestoreHistoryRecord>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM RestoreHistory
            ORDER BY RestoreTimestamp DESC
            LIMIT @Limit;
        ";

        var rows = await connection.QueryAsync<RestoreHistoryRow>(sql, new { Limit = limit });
        return rows.Select(MapToEntity).ToList();
    }

    private static RestoreHistoryRecord MapToEntity(RestoreHistoryRow row)
    {
        return new RestoreHistoryRecord
        {
            Id = row.Id,
            DatabaseName = row.DatabaseName,
            RestoreTimestamp = DateTime.Parse(row.RestoreTimestamp, null, DateTimeStyles.RoundtripKind),
            TargetRestoreTime = DateTime.Parse(row.TargetRestoreTime, null, DateTimeStyles.RoundtripKind),
            FullBackupFile = row.FullBackupFile,
            DiffBackupFile = row.DiffBackupFile,
            LogBackupFiles = ParseLogFiles(row.LogBackupFilesJson),
            Success = row.Success != 0,
            DurationMs = row.DurationMs,
            ErrorMessage = row.ErrorMessage
        };
    }

    private static IReadOnlyList<string> ParseLogFiles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private sealed class RestoreHistoryRow
    {
        public long Id { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string RestoreTimestamp { get; set; } = string.Empty;
        public string TargetRestoreTime { get; set; } = string.Empty;
        public string FullBackupFile { get; set; } = string.Empty;
        public string? DiffBackupFile { get; set; }
        public string LogBackupFilesJson { get; set; } = "[]";
        public int Success { get; set; }
        public long DurationMs { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
