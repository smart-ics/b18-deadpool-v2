using Dapper;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence;

/// <summary>
/// SQLite-based backup job repository for shared Agent/UI data store.
/// </summary>
public class SqliteBackupJobRepository : IBackupJobRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteBackupJobRepository> _logger;

    public SqliteBackupJobRepository(string databasePath, ILogger<SqliteBackupJobRepository> logger)
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

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS BackupJobs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DatabaseName TEXT NOT NULL,
                BackupType INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                ExecutionStartTime TEXT,
                EndTime TEXT,
                BackupFilePath TEXT NOT NULL,
                FileSizeBytes INTEGER,
                ErrorMessage TEXT,
                FirstLSN REAL,
                LastLSN REAL,
                DatabaseBackupLSN REAL,
                CheckpointLSN REAL
            );

            CREATE INDEX IF NOT EXISTS idx_backupjobs_database_starttime 
            ON BackupJobs(DatabaseName, StartTime DESC);

            CREATE INDEX IF NOT EXISTS idx_backupjobs_database_type_status 
            ON BackupJobs(DatabaseName, BackupType, Status);
        ";

        connection.Execute(createTableSql);
        EnsureColumnExists(connection, "BackupJobs", "ExecutionStartTime", "TEXT");
        _logger.LogInformation("SQLite backup job database initialized at {ConnectionString}", _connectionString);
    }

    private static void EnsureColumnExists(SqliteConnection connection, string tableName, string columnName, string columnType)
    {
        var existingColumns = connection.Query<TableInfoRow>($"PRAGMA table_info({tableName});")
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (existingColumns.Contains(columnName))
            return;

        connection.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};");
    }

    private sealed class TableInfoRow
    {
        public string Name { get; set; } = "";
    }

    public async Task CreateAsync(BackupJob backupJob)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO BackupJobs (
                DatabaseName, BackupType, Status, StartTime, EndTime, 
                BackupFilePath, FileSizeBytes, ErrorMessage,
                FirstLSN, LastLSN, DatabaseBackupLSN, CheckpointLSN,
                ExecutionStartTime
            ) VALUES (
                @DatabaseName, @BackupType, @Status, @StartTime, @EndTime,
                @BackupFilePath, @FileSizeBytes, @ErrorMessage,
                @FirstLSN, @LastLSN, @DatabaseBackupLSN, @CheckpointLSN,
                @ExecutionStartTime
            );
        ";

        await connection.ExecuteAsync(sql, new
        {
            backupJob.DatabaseName,
            BackupType = (int)backupJob.BackupType,
            Status = (int)backupJob.Status,
            StartTime = backupJob.StartTime.ToString("O"),
            ExecutionStartTime = backupJob.ExecutionStartTime?.ToString("O"),
            EndTime = backupJob.EndTime?.ToString("O"),
            backupJob.BackupFilePath,
            backupJob.FileSizeBytes,
            backupJob.ErrorMessage,
            backupJob.FirstLSN,
            backupJob.LastLSN,
            backupJob.DatabaseBackupLSN,
            backupJob.CheckpointLSN
        });
    }

    public async Task UpdateAsync(BackupJob backupJob)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            UPDATE BackupJobs SET
                Status = @Status,
                ExecutionStartTime = @ExecutionStartTime,
                EndTime = @EndTime,
                BackupFilePath = @BackupFilePath,
                FileSizeBytes = @FileSizeBytes,
                ErrorMessage = @ErrorMessage,
                FirstLSN = @FirstLSN,
                LastLSN = @LastLSN,
                DatabaseBackupLSN = @DatabaseBackupLSN,
                CheckpointLSN = @CheckpointLSN
            WHERE DatabaseName = @DatabaseName
              AND BackupType = @BackupType
              AND StartTime = @StartTime;
        ";

        await connection.ExecuteAsync(sql, new
        {
            backupJob.DatabaseName,
            BackupType = (int)backupJob.BackupType,
            StartTime = backupJob.StartTime.ToString("O"),
            Status = (int)backupJob.Status,
            ExecutionStartTime = backupJob.ExecutionStartTime?.ToString("O"),
            EndTime = backupJob.EndTime?.ToString("O"),
            backupJob.BackupFilePath,
            backupJob.FileSizeBytes,
            backupJob.ErrorMessage,
            backupJob.FirstLSN,
            backupJob.LastLSN,
            backupJob.DatabaseBackupLSN,
            backupJob.CheckpointLSN
        });
    }

    public async Task<BackupJob?> GetByIdAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM BackupJobs WHERE Id = @Id;";
        var row = await connection.QuerySingleOrDefaultAsync<BackupJobRow>(sql, new { Id = id });

        return row != null ? MapToBackupJob(row) : null;
    }

    public async Task<IEnumerable<BackupJob>> GetRecentJobsAsync(string databaseName, int count)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupJobs 
            WHERE DatabaseName = @DatabaseName 
            ORDER BY StartTime DESC 
            LIMIT @Count;
        ";

        var rows = await connection.QueryAsync<BackupJobRow>(sql, new { DatabaseName = databaseName, Count = count });
        return rows.Select(MapToBackupJob).ToList();
    }

    public async Task<BackupJob?> GetLastSuccessfulFullBackupAsync(string databaseName)
    {
        return await GetLastSuccessfulBackupAsync(databaseName, BackupType.Full);
    }

    public async Task<bool> HasSuccessfulFullBackupAsync(string databaseName)
    {
        var lastFull = await GetLastSuccessfulFullBackupAsync(databaseName);
        return lastFull != null;
    }

    public async Task<IEnumerable<BackupJob>> GetPendingJobsAsync(int maxCount)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupJobs 
            WHERE Status = @Status 
            ORDER BY StartTime 
            LIMIT @MaxCount;
        ";

        var rows = await connection.QueryAsync<BackupJobRow>(sql, new
        {
            Status = (int)BackupStatus.Pending,
            MaxCount = maxCount
        });

        return rows.Select(MapToBackupJob).ToList();
    }

    public async Task<bool> TryClaimJobAsync(BackupJob job)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            UPDATE BackupJobs 
            SET Status = @NewStatus 
            WHERE DatabaseName = @DatabaseName
            AND BackupType = @BackupType
            AND StartTime = @StartTime
            AND Status = @OldStatus
            AND NOT EXISTS (
                SELECT 1
                FROM BackupJobs r
                WHERE r.DatabaseName = @DatabaseName
                AND r.Status = @RunningStatus
            );
        ";

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            job.DatabaseName,
            BackupType = (int)job.BackupType,
            StartTime = job.StartTime.ToString("O"),
            NewStatus = (int)BackupStatus.Running,
            OldStatus = (int)BackupStatus.Pending,
            RunningStatus = (int)BackupStatus.Running
        });

        return rowsAffected > 0;
    }

    public async Task<bool> HasRunningJobAsync(string databaseName)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT EXISTS(
                SELECT 1 FROM BackupJobs
                WHERE DatabaseName = @DatabaseName
                AND Status = @Status
            );
        ";

        var exists = await connection.ExecuteScalarAsync<long>(sql, new
        {
            DatabaseName = databaseName,
            Status = (int)BackupStatus.Running
        });

        return exists == 1;
    }

    public async Task<BackupJob?> GetLastSuccessfulBackupAsync(string databaseName, BackupType backupType)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupJobs 
            WHERE DatabaseName = @DatabaseName 
            AND BackupType = @BackupType 
            AND Status = @Status 
            ORDER BY StartTime DESC 
            LIMIT 1;
        ";

        var row = await connection.QuerySingleOrDefaultAsync<BackupJobRow>(sql, new
        {
            DatabaseName = databaseName,
            BackupType = (int)backupType,
            Status = (int)BackupStatus.Completed
        });

        return row != null ? MapToBackupJob(row) : null;
    }

    public async Task<BackupJob?> GetLastFailedBackupAsync(string databaseName)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupJobs 
            WHERE DatabaseName = @DatabaseName 
            AND Status = @Status 
            ORDER BY StartTime DESC 
            LIMIT 1;
        ";

        var row = await connection.QuerySingleOrDefaultAsync<BackupJobRow>(sql, new
        {
            DatabaseName = databaseName,
            Status = (int)BackupStatus.Failed
        });

        return row != null ? MapToBackupJob(row) : null;
    }

    public async Task<IEnumerable<BackupJob>> GetBackupChainAsync(string databaseName, DateTime since)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupJobs 
            WHERE DatabaseName = @DatabaseName 
            AND StartTime >= @Since 
            AND Status = @Status 
            ORDER BY StartTime;
        ";

        var rows = await connection.QueryAsync<BackupJobRow>(sql, new
        {
            DatabaseName = databaseName,
            Since = since.ToString("O"),
            Status = (int)BackupStatus.Completed
        });

        return rows.Select(MapToBackupJob).ToList();
    }

    public async Task<IEnumerable<BackupJob>> GetBackupsByDatabaseAsync(string databaseName)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT * FROM BackupJobs 
            WHERE DatabaseName = @DatabaseName 
            ORDER BY StartTime DESC;
        ";

        var rows = await connection.QueryAsync<BackupJobRow>(sql, new { DatabaseName = databaseName });
        return rows.Select(MapToBackupJob).ToList();
    }

    private BackupJob MapToBackupJob(BackupJobRow row)
    {
        var backupType = (BackupType)row.BackupType;
        var status = (BackupStatus)row.Status;
        var startTime = DateTime.Parse(row.StartTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var endTime = string.IsNullOrWhiteSpace(row.EndTime)
            ? (DateTime?)null
            : DateTime.Parse(row.EndTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var executionStartTime = string.IsNullOrWhiteSpace(row.ExecutionStartTime)
            ? (DateTime?)null
            : DateTime.Parse(row.ExecutionStartTime, null, System.Globalization.DateTimeStyles.RoundtripKind);

        return BackupJob.Restore(
            row.DatabaseName,
            backupType,
            status,
            startTime,
            endTime,
            row.BackupFilePath,
            row.FileSizeBytes,
            row.ErrorMessage,
            row.FirstLSN,
            row.LastLSN,
            row.DatabaseBackupLSN,
            row.CheckpointLSN,
            executionStartTime);
    }

    private class BackupJobRow
    {
        public int Id { get; set; }
        public string DatabaseName { get; set; } = "";
        public int BackupType { get; set; }
        public int Status { get; set; }
        public string StartTime { get; set; } = "";
        public string? ExecutionStartTime { get; set; }
        public string? EndTime { get; set; }
        public string BackupFilePath { get; set; } = "";
        public long? FileSizeBytes { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal? FirstLSN { get; set; }
        public decimal? LastLSN { get; set; }
        public decimal? DatabaseBackupLSN { get; set; }
        public decimal? CheckpointLSN { get; set; }
    }
}
