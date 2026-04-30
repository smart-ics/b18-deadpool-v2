using System.Globalization;
using System.Text.Json;
using Dapper;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence;

public class SqliteStorageHealthCheckRepository : IStorageHealthCheckRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteStorageHealthCheckRepository> _logger;

    public SqliteStorageHealthCheckRepository(string databasePath, ILogger<SqliteStorageHealthCheckRepository> logger)
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
            CREATE TABLE IF NOT EXISTS StorageHealthChecks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VolumePath TEXT NOT NULL,
                CheckTime TEXT NOT NULL,
                TotalBytes INTEGER NOT NULL,
                FreeBytes INTEGER NOT NULL,
                OverallHealth INTEGER NOT NULL,
                Warnings TEXT NOT NULL,
                CriticalFindings TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_storagehealthchecks_volume_checktime
            ON StorageHealthChecks(VolumePath, CheckTime DESC);
        ";

        connection.Execute(sql);
        _logger.LogInformation("SQLite storage health check database initialized at {ConnectionString}", _connectionString);
    }

    public async Task CreateAsync(StorageHealthCheck healthCheck)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO StorageHealthChecks (
                VolumePath, CheckTime, TotalBytes, FreeBytes,
                OverallHealth, Warnings, CriticalFindings
            ) VALUES (
                @VolumePath, @CheckTime, @TotalBytes, @FreeBytes,
                @OverallHealth, @Warnings, @CriticalFindings
            );
        ";

        await connection.ExecuteAsync(sql, new
        {
            healthCheck.VolumePath,
            CheckTime = healthCheck.CheckTime.ToString("O"),
            healthCheck.TotalBytes,
            healthCheck.FreeBytes,
            OverallHealth = (int)healthCheck.OverallHealth,
            Warnings = JsonSerializer.Serialize(healthCheck.Warnings),
            CriticalFindings = JsonSerializer.Serialize(healthCheck.CriticalFindings)
        });
    }

    public async Task<StorageHealthCheck?> GetLatestHealthCheckAsync(string volumePath)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM StorageHealthChecks
            WHERE VolumePath = @VolumePath
            ORDER BY CheckTime DESC
            LIMIT 1;
        ";

        var row = await connection.QuerySingleOrDefaultAsync<StorageHealthCheckRow>(
            sql, new { VolumePath = volumePath });

        return row != null ? MapToEntity(row) : null;
    }

    public async Task<IEnumerable<StorageHealthCheck>> GetRecentHealthChecksAsync(string volumePath, int count)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM StorageHealthChecks
            WHERE VolumePath = @VolumePath
            ORDER BY CheckTime DESC
            LIMIT @Count;
        ";

        var rows = await connection.QueryAsync<StorageHealthCheckRow>(
            sql, new { VolumePath = volumePath, Count = count });

        return rows.Select(MapToEntity).ToList();
    }

    public void CleanupOldHealthChecks(TimeSpan retention)
    {
        try
        {
            var cutoff = DateTime.UtcNow.Subtract(retention).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = "DELETE FROM StorageHealthChecks WHERE CheckTime < @Cutoff;";
            var deleted = connection.Execute(sql, new { Cutoff = cutoff });

            if (deleted > 0)
                _logger.LogDebug("Cleaned up {Count} old storage health check records.", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old storage health check records.");
        }
    }

    private static StorageHealthCheck MapToEntity(StorageHealthCheckRow row)
    {
        var checkTime = DateTime.Parse(row.CheckTime, null, DateTimeStyles.RoundtripKind);
        var warnings = JsonSerializer.Deserialize<List<string>>(row.Warnings) ?? new List<string>();
        var criticalFindings = JsonSerializer.Deserialize<List<string>>(row.CriticalFindings) ?? new List<string>();

        return StorageHealthCheck.Restore(
            row.VolumePath,
            checkTime,
            row.TotalBytes,
            row.FreeBytes,
            (HealthStatus)row.OverallHealth,
            warnings,
            criticalFindings);
    }

    private class StorageHealthCheckRow
    {
        public int Id { get; set; }
        public string VolumePath { get; set; } = string.Empty;
        public string CheckTime { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public int OverallHealth { get; set; }
        public string Warnings { get; set; } = "[]";
        public string CriticalFindings { get; set; } = "[]";
    }
}
