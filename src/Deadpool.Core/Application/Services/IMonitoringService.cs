using Deadpool.Core.Domain.Common;
using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Application.Services;

/// <summary>
/// Service for monitoring SQL Server instances and databases
/// </summary>
public interface IMonitoringService
{
    /// <summary>
    /// Check connectivity to a SQL Server instance
    /// </summary>
    Task<Result<bool>> CheckServerConnectivityAsync(SqlServerInstance server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover databases on a SQL Server instance
    /// </summary>
    Task<Result<IEnumerable<DatabaseInfo>>> DiscoverDatabasesAsync(SqlServerInstance server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get backup history for a database
    /// </summary>
    Task<Result<IEnumerable<BackupHistoryInfo>>> GetBackupHistoryAsync(SqlServerInstance server, string databaseName, int days = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if database requires backup based on policy
    /// </summary>
    Task<Result<BackupHealthStatus>> CheckBackupHealthAsync(Database database, SqlServerInstance server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get SQL Server version and edition information
    /// </summary>
    Task<Result<ServerInfo>> GetServerInfoAsync(SqlServerInstance server, CancellationToken cancellationToken = default);
}

/// <summary>
/// Discovered database information
/// </summary>
public record DatabaseInfo(
    string Name,
    string RecoveryModel,
    long SizeInBytes,
    bool IsOnline,
    DateTime? LastBackupDate,
    DateTime? LastLogBackupDate,
    string? Collation,
    int CompatibilityLevel
);

/// <summary>
/// Backup history information from msdb
/// </summary>
public record BackupHistoryInfo(
    string BackupType,
    DateTime BackupStartDate,
    DateTime BackupFinishDate,
    long BackupSizeInBytes,
    string BackupFilePath,
    bool IsCompressed
);

/// <summary>
/// Backup health status for a database
/// </summary>
public record BackupHealthStatus(
    bool IsHealthy,
    DateTime? LastFullBackup,
    DateTime? LastDifferentialBackup,
    DateTime? LastLogBackup,
    List<string> Warnings,
    List<string> Errors
);

/// <summary>
/// SQL Server instance information
/// </summary>
public record ServerInfo(
    string Version,
    string Edition,
    string ProductLevel,
    bool IsClusteredInstance,
    bool IsHadrEnabled
);
