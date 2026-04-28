using Deadpool.Core.Domain.Common;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Application.Services;

/// <summary>
/// Service for executing SQL Server backup operations
/// </summary>
public interface IBackupExecutionService
{
    /// <summary>
    /// Execute a backup job
    /// </summary>
    Task<Result> ExecuteBackupAsync(BackupJob job, Database database, SqlServerInstance server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify a backup file integrity
    /// </summary>
    Task<Result> VerifyBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get backup file information without restoring
    /// </summary>
    Task<Result<BackupFileInfo>> GetBackupInfoAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimate the size of a backup before execution
    /// </summary>
    Task<Result<long>> EstimateBackupSizeAsync(Database database, BackupType backupType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a backup file
/// </summary>
public record BackupFileInfo(
    string DatabaseName,
    BackupType BackupType,
    DateTime BackupStartDate,
    DateTime BackupFinishDate,
    long BackupSize,
    bool IsCompressed,
    string ServerName,
    string? RecoveryModel
);
