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
        _logger.LogInformation("SQLite backup job database initialized at {ConnectionString}", _connectionString);
    }

    public async Task CreateAsync(BackupJob backupJob)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO BackupJobs (
                DatabaseName, BackupType, Status, StartTime, EndTime, 
                BackupFilePath, FileSizeBytes, ErrorMessage,
                FirstLSN, LastLSN, DatabaseBackupLSN, CheckpointLSN
            ) VALUES (
                @DatabaseName, @BackupType, @Status, @StartTime, @EndTime,
                @BackupFilePath, @FileSizeBytes, @ErrorMessage,
                @FirstLSN, @LastLSN, @DatabaseBackupLSN, @CheckpointLSN
            );
        ";

        await connection.ExecuteAsync(sql, new
        {
            backupJob.DatabaseName,
            BackupType = (int)backupJob.BackupType,
            Status = (int)backupJob.Status,
            StartTime = backupJob.StartTime.ToString("O"),
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
                EndTime = @EndTime,
                FileSizeBytes = @FileSizeBytes,
                ErrorMessage = @ErrorMessage,
                FirstLSN = @FirstLSN,
                LastLSN = @LastLSN,
                DatabaseBackupLSN = @DatabaseBackupLSN,
                CheckpointLSN = @CheckpointLSN
            WHERE BackupFilePath = @BackupFilePath;
        ";

        await connection.ExecuteAsync(sql, new
        {
            Status = (int)backupJob.Status,
            EndTime = backupJob.EndTime?.ToString("O"),
            backupJob.FileSizeBytes,
            backupJob.ErrorMessage,
            backupJob.FirstLSN,
            backupJob.LastLSN,
            backupJob.DatabaseBackupLSN,
            backupJob.CheckpointLSN,
            backupJob.BackupFilePath
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
            WHERE BackupFilePath = @BackupFilePath 
            AND Status = @OldStatus;
        ";

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            NewStatus = (int)BackupStatus.Running,
            OldStatus = (int)BackupStatus.Pending,
            job.BackupFilePath
        });

        return rowsAffected > 0;
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
        var job = new BackupJob(
            row.DatabaseName,
            (BackupType)row.BackupType,
            row.BackupFilePath);

        // Use reflection to set readonly StartTime
        typeof(BackupJob).GetProperty(nameof(BackupJob.StartTime))!
            .SetValue(job, DateTime.Parse(row.StartTime));

        // Set status
        var statusField = typeof(BackupJob).GetProperty("Status", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
        statusField.SetValue(job, (BackupStatus)row.Status);

        // Set EndTime if available
        if (!string.IsNullOrEmpty(row.EndTime))
        {
            typeof(BackupJob).GetProperty(nameof(BackupJob.EndTime))!
                .SetValue(job, DateTime.Parse(row.EndTime));
        }

        // Set FileSizeBytes if available
        if (row.FileSizeBytes.HasValue)
        {
            typeof(BackupJob).GetProperty(nameof(BackupJob.FileSizeBytes))!
                .SetValue(job, row.FileSizeBytes);
        }

        // Set ErrorMessage if available
        if (!string.IsNullOrEmpty(row.ErrorMessage))
        {
            typeof(BackupJob).GetProperty(nameof(BackupJob.ErrorMessage))!
                .SetValue(job, row.ErrorMessage);
        }

        // Set LSN metadata if available
        if (row.FirstLSN.HasValue || row.LastLSN.HasValue || row.DatabaseBackupLSN.HasValue || row.CheckpointLSN.HasValue)
        {
            typeof(BackupJob).GetProperty(nameof(BackupJob.FirstLSN))!.SetValue(job, row.FirstLSN);
            typeof(BackupJob).GetProperty(nameof(BackupJob.LastLSN))!.SetValue(job, row.LastLSN);
            typeof(BackupJob).GetProperty(nameof(BackupJob.DatabaseBackupLSN))!.SetValue(job, row.DatabaseBackupLSN);
            typeof(BackupJob).GetProperty(nameof(BackupJob.CheckpointLSN))!.SetValue(job, row.CheckpointLSN);
        }

        return job;
    }

    private class BackupJobRow
    {
        public int Id { get; set; }
        public string DatabaseName { get; set; } = "";
        public int BackupType { get; set; }
        public int Status { get; set; }
        public string StartTime { get; set; } = "";
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
