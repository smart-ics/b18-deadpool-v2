using System.Globalization;
using System.Text.Json;
using Dapper;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence;

public class SqliteBackupHealthCheckRepository : IBackupHealthCheckRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteBackupHealthCheckRepository> _logger;

    public SqliteBackupHealthCheckRepository(string databasePath, ILogger<SqliteBackupHealthCheckRepository> logger)
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
            CREATE TABLE IF NOT EXISTS BackupHealthChecks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DatabaseName TEXT NOT NULL,
                CheckTime TEXT NOT NULL,
                OverallHealth INTEGER NOT NULL,
                LastSuccessfulFullBackup TEXT,
                LastSuccessfulDifferentialBackup TEXT,
                LastSuccessfulLogBackup TEXT,
                LastFailedBackup TEXT,
                Warnings TEXT NOT NULL,
                CriticalFindings TEXT NOT NULL,
                Limitations TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_backuphealthchecks_db_checktime
            ON BackupHealthChecks(DatabaseName, CheckTime DESC);
        ";

        connection.Execute(sql);
        _logger.LogInformation("SQLite backup health check database initialized at {ConnectionString}", _connectionString);
    }

    public async Task CreateAsync(BackupHealthCheck healthCheck)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO BackupHealthChecks (
                DatabaseName, CheckTime, OverallHealth,
                LastSuccessfulFullBackup, LastSuccessfulDifferentialBackup,
                LastSuccessfulLogBackup, LastFailedBackup,
                Warnings, CriticalFindings, Limitations
            ) VALUES (
                @DatabaseName, @CheckTime, @OverallHealth,
                @LastSuccessfulFullBackup, @LastSuccessfulDifferentialBackup,
                @LastSuccessfulLogBackup, @LastFailedBackup,
                @Warnings, @CriticalFindings, @Limitations
            );
        ";

        await connection.ExecuteAsync(sql, new
        {
            healthCheck.DatabaseName,
            CheckTime = healthCheck.CheckTime.ToString("O"),
            OverallHealth = (int)healthCheck.OverallHealth,
            LastSuccessfulFullBackup = healthCheck.LastSuccessfulFullBackup?.ToString("O"),
            LastSuccessfulDifferentialBackup = healthCheck.LastSuccessfulDifferentialBackup?.ToString("O"),
            LastSuccessfulLogBackup = healthCheck.LastSuccessfulLogBackup?.ToString("O"),
            LastFailedBackup = healthCheck.LastFailedBackup?.ToString("O"),
            Warnings = JsonSerializer.Serialize(healthCheck.Warnings),
            CriticalFindings = JsonSerializer.Serialize(healthCheck.CriticalFindings),
            Limitations = JsonSerializer.Serialize(healthCheck.Limitations)
        });
    }

    public async Task<BackupHealthCheck?> GetLatestHealthCheckAsync(string databaseName)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupHealthChecks
            WHERE DatabaseName = @DatabaseName
            ORDER BY CheckTime DESC
            LIMIT 1;
        ";

        var row = await connection.QuerySingleOrDefaultAsync<BackupHealthCheckRow>(
            sql, new { DatabaseName = databaseName });

        return row != null ? MapToEntity(row) : null;
    }

    public async Task<IEnumerable<BackupHealthCheck>> GetRecentHealthChecksAsync(string databaseName, int count)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupHealthChecks
            WHERE DatabaseName = @DatabaseName
            ORDER BY CheckTime DESC
            LIMIT @Count;
        ";

        var rows = await connection.QueryAsync<BackupHealthCheckRow>(
            sql, new { DatabaseName = databaseName, Count = count });

        return rows.Select(MapToEntity).ToList();
    }

    public void CleanupOldHealthChecks(TimeSpan retention)
    {
        try
        {
            var cutoff = DateTime.UtcNow.Subtract(retention).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = "DELETE FROM BackupHealthChecks WHERE CheckTime < @Cutoff;";
            var deleted = connection.Execute(sql, new { Cutoff = cutoff });

            if (deleted > 0)
                _logger.LogDebug("Cleaned up {Count} old backup health check records.", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old backup health check records.");
        }
    }

    private static BackupHealthCheck MapToEntity(BackupHealthCheckRow row)
    {
        var checkTime = DateTime.Parse(row.CheckTime, null, DateTimeStyles.RoundtripKind);
        var warnings = JsonSerializer.Deserialize<List<string>>(row.Warnings) ?? new List<string>();
        var criticalFindings = JsonSerializer.Deserialize<List<string>>(row.CriticalFindings) ?? new List<string>();
        var limitations = JsonSerializer.Deserialize<List<string>>(row.Limitations) ?? new List<string>();

        return BackupHealthCheck.Restore(
            row.DatabaseName,
            checkTime,
            (HealthStatus)row.OverallHealth,
            ParseNullableDate(row.LastSuccessfulFullBackup),
            ParseNullableDate(row.LastSuccessfulDifferentialBackup),
            ParseNullableDate(row.LastSuccessfulLogBackup),
            ParseNullableDate(row.LastFailedBackup),
            warnings,
            criticalFindings,
            limitations);
    }

    private static DateTime? ParseNullableDate(string? value)
        => value != null ? DateTime.Parse(value, null, DateTimeStyles.RoundtripKind) : null;

    private class BackupHealthCheckRow
    {
        public int Id { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string CheckTime { get; set; } = string.Empty;
        public int OverallHealth { get; set; }
        public string? LastSuccessfulFullBackup { get; set; }
        public string? LastSuccessfulDifferentialBackup { get; set; }
        public string? LastSuccessfulLogBackup { get; set; }
        public string? LastFailedBackup { get; set; }
        public string Warnings { get; set; } = "[]";
        public string CriticalFindings { get; set; } = "[]";
        public string Limitations { get; set; } = "[]";
    }
}
