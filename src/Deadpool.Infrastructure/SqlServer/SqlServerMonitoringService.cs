using Dapper;
using Deadpool.Core.Application.Services;
using Deadpool.Core.Domain.Common;
using Deadpool.Core.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.SqlServer;

/// <summary>
/// SQL Server monitoring service implementation
/// </summary>
public class SqlServerMonitoringService : IMonitoringService
{
    private readonly ILogger<SqlServerMonitoringService> _logger;

    public SqlServerMonitoringService(ILogger<SqlServerMonitoringService> logger)
    {
        _logger = logger;
    }

    public async Task<Result<bool>> CheckServerConnectivityAsync(
        SqlServerInstance server, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(server.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            
            _logger.LogInformation("Successfully connected to {Server}", server.GetFullServerName());
            return Result.Success(true);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to {Server}", server.GetFullServerName());
            return Result.Failure<bool>($"Connection failed: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<DatabaseInfo>>> DiscoverDatabasesAsync(
        SqlServerInstance server, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT 
                    d.name AS Name,
                    d.recovery_model_desc AS RecoveryModel,
                    SUM(mf.size * 8 * 1024) AS SizeInBytes,
                    CAST(CASE WHEN d.state = 0 THEN 1 ELSE 0 END AS BIT) AS IsOnline,
                    d.collation_name AS Collation,
                    d.compatibility_level AS CompatibilityLevel,
                    (SELECT MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE database_name = d.name AND type = 'D') AS LastBackupDate,
                    (SELECT MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE database_name = d.name AND type = 'L') AS LastLogBackupDate
                FROM sys.databases d
                LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id
                WHERE d.database_id > 4  -- Exclude system databases
                GROUP BY d.name, d.recovery_model_desc, d.state, d.collation_name, d.compatibility_level
                ORDER BY d.name";

            await using var connection = new SqlConnection(server.ConnectionString);
            var results = await connection.QueryAsync<DatabaseInfo>(sql);

            _logger.LogInformation("Discovered {Count} databases on {Server}", results.Count(), server.GetFullServerName());
            return Result.Success(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering databases on {Server}", server.GetFullServerName());
            return Result.Failure<IEnumerable<DatabaseInfo>>($"Discovery failed: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<BackupHistoryInfo>>> GetBackupHistoryAsync(
        SqlServerInstance server, 
        string databaseName, 
        int days = 30, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT 
                    CASE bs.type 
                        WHEN 'D' THEN 'Full'
                        WHEN 'I' THEN 'Differential'
                        WHEN 'L' THEN 'Log'
                    END AS BackupType,
                    bs.backup_start_date AS BackupStartDate,
                    bs.backup_finish_date AS BackupFinishDate,
                    bs.backup_size AS BackupSizeInBytes,
                    bmf.physical_device_name AS BackupFilePath,
                    CAST(bs.compressed_backup_size AS BIT) AS IsCompressed
                FROM msdb.dbo.backupset bs
                INNER JOIN msdb.dbo.backupmediafamily bmf ON bs.media_set_id = bmf.media_set_id
                WHERE bs.database_name = @DatabaseName
                AND bs.backup_start_date >= DATEADD(day, -@Days, GETDATE())
                ORDER BY bs.backup_start_date DESC";

            await using var connection = new SqlConnection(server.ConnectionString);
            var results = await connection.QueryAsync<BackupHistoryInfo>(sql, new { DatabaseName = databaseName, Days = days });

            return Result.Success(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup history for {Database}", databaseName);
            return Result.Failure<IEnumerable<BackupHistoryInfo>>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<BackupHealthStatus>> CheckBackupHealthAsync(
        Database database, 
        SqlServerInstance server, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var warnings = new List<string>();
            var errors = new List<string>();

            var historyResult = await GetBackupHistoryAsync(server, database.Name, 30, cancellationToken);
            if (historyResult.IsFailure)
            {
                errors.Add($"Could not retrieve backup history: {historyResult.Error}");
                return Result.Success(new BackupHealthStatus(false, null, null, null, warnings, errors));
            }

            var history = historyResult.Value.ToList();
            var lastFull = history.FirstOrDefault(h => h.BackupType == "Full")?.BackupFinishDate;
            var lastDiff = history.FirstOrDefault(h => h.BackupType == "Differential")?.BackupFinishDate;
            var lastLog = history.FirstOrDefault(h => h.BackupType == "Log")?.BackupFinishDate;

            // Check backup freshness
            if (!lastFull.HasValue)
            {
                errors.Add("No full backup found in the last 30 days");
            }
            else if (DateTime.UtcNow - lastFull.Value > TimeSpan.FromDays(7))
            {
                warnings.Add($"Last full backup is {(DateTime.UtcNow - lastFull.Value).Days} days old");
            }

            if (database.SupportsLogBackups() && !lastLog.HasValue)
            {
                warnings.Add("Database supports log backups but none found in last 30 days");
            }

            bool isHealthy = errors.Count == 0;
            return Result.Success(new BackupHealthStatus(isHealthy, lastFull, lastDiff, lastLog, warnings, errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking backup health for {Database}", database.Name);
            return Result.Failure<BackupHealthStatus>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<ServerInfo>> GetServerInfoAsync(
        SqlServerInstance server, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT 
                    CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR) AS Version,
                    CAST(SERVERPROPERTY('Edition') AS NVARCHAR) AS Edition,
                    CAST(SERVERPROPERTY('ProductLevel') AS NVARCHAR) AS ProductLevel,
                    CAST(SERVERPROPERTY('IsClustered') AS BIT) AS IsClusteredInstance,
                    CAST(SERVERPROPERTY('IsHadrEnabled') AS BIT) AS IsHadrEnabled";

            await using var connection = new SqlConnection(server.ConnectionString);
            var info = await connection.QuerySingleAsync<ServerInfo>(sql);

            return Result.Success(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting server info for {Server}", server.GetFullServerName());
            return Result.Failure<ServerInfo>($"Error: {ex.Message}");
        }
    }
}
