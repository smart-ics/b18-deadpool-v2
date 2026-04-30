using System.Globalization;
using Dapper;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence;

public class SqliteDatabasePulseRepository : IDatabasePulseRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDatabasePulseRepository> _logger;

    public SqliteDatabasePulseRepository(string databasePath, ILogger<SqliteDatabasePulseRepository> logger)
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
            CREATE TABLE IF NOT EXISTS DatabasePulseRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CheckTime TEXT NOT NULL,
                Status INTEGER NOT NULL,
                ErrorMessage TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_databasepulserecords_checktime
            ON DatabasePulseRecords(CheckTime DESC);
        ";

        connection.Execute(sql);
        _logger.LogInformation("SQLite database pulse repository initialized at {ConnectionString}", _connectionString);
    }

    public async Task CreateAsync(DatabasePulseRecord record)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO DatabasePulseRecords (CheckTime, Status, ErrorMessage)
            VALUES (@CheckTime, @Status, @ErrorMessage);
        ";

        await connection.ExecuteAsync(sql, new
        {
            CheckTime = record.CheckTime.ToString("O"),
            Status = (int)record.Status,
            record.ErrorMessage
        });
    }

    public async Task<DatabasePulseRecord?> GetLatestAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM DatabasePulseRecords
            ORDER BY CheckTime DESC
            LIMIT 1;
        ";

        var row = await connection.QuerySingleOrDefaultAsync<DatabasePulseRow>(sql);
        return row != null ? MapToEntity(row) : null;
    }

    public void CleanupOldRecords(TimeSpan retention)
    {
        try
        {
            var cutoff = DateTime.UtcNow.Subtract(retention).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = "DELETE FROM DatabasePulseRecords WHERE CheckTime < @Cutoff;";
            var deleted = connection.Execute(sql, new { Cutoff = cutoff });

            if (deleted > 0)
                _logger.LogDebug("Cleaned up {Count} old database pulse records.", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old database pulse records.");
        }
    }

    private static DatabasePulseRecord MapToEntity(DatabasePulseRow row)
    {
        var checkTime = DateTime.Parse(row.CheckTime, null, DateTimeStyles.RoundtripKind);
        return new DatabasePulseRecord(checkTime, (HealthStatus)row.Status, row.ErrorMessage);
    }

    private class DatabasePulseRow
    {
        public int Id { get; set; }
        public string CheckTime { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
