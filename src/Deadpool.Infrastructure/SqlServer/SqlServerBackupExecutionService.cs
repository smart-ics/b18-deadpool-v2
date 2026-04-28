using Deadpool.Core.Application.Services;
using Deadpool.Core.Domain.Common;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Deadpool.Infrastructure.SqlServer;

/// <summary>
/// SQL Server backup execution implementation
/// </summary>
public class SqlServerBackupExecutionService : IBackupExecutionService
{
    private readonly ILogger<SqlServerBackupExecutionService> _logger;

    public SqlServerBackupExecutionService(ILogger<SqlServerBackupExecutionService> logger)
    {
        _logger = logger;
    }

    public async Task<Result> ExecuteBackupAsync(
        BackupJob job, 
        Database database, 
        SqlServerInstance server, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Starting {BackupType} backup for database {Database} on {Server}", 
                job.BackupType, 
                database.Name, 
                server.GetFullServerName());

            var backupCommand = BuildBackupCommand(job, database);
            
            await using var connection = new SqlConnection(server.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(backupCommand, connection)
            {
                CommandTimeout = 0 // No timeout for backup operations
            };

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Completed {BackupType} backup for database {Database}", 
                job.BackupType, 
                database.Name);

            return Result.Success();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, 
                "SQL error during backup of {Database}: {Message}", 
                database.Name, 
                ex.Message);
            return Result.Failure($"SQL Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error during backup of {Database}: {Message}", 
                database.Name, 
                ex.Message);
            return Result.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<Result> VerifyBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verifying backup file: {FilePath}", backupFilePath);

            // For now, just check file existence
            // TODO: Implement RESTORE VERIFYONLY
            if (!File.Exists(backupFilePath))
            {
                return Result.Failure($"Backup file not found: {backupFilePath}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying backup: {Message}", ex.Message);
            return Result.Failure($"Verification failed: {ex.Message}");
        }
    }

    public async Task<Result<BackupFileInfo>> GetBackupInfoAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Implement RESTORE HEADERONLY to read backup metadata
            _logger.LogInformation("Getting backup info for: {FilePath}", backupFilePath);
            
            return Result.Failure<BackupFileInfo>("Not yet implemented");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup info: {Message}", ex.Message);
            return Result.Failure<BackupFileInfo>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<long>> EstimateBackupSizeAsync(
        Database database, 
        BackupType backupType, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Rough estimation: Full backup ~= database size, others smaller
            var estimatedSize = backupType switch
            {
                BackupType.Full => database.SizeInBytes,
                BackupType.Differential => database.SizeInBytes / 4, // Rough estimate
                BackupType.Log => database.SizeInBytes / 10, // Rough estimate
                _ => database.SizeInBytes
            };

            return Result.Success(estimatedSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating backup size: {Message}", ex.Message);
            return Result.Failure<long>($"Error: {ex.Message}");
        }
    }

    private string BuildBackupCommand(BackupJob job, Database database)
    {
        var backupType = job.BackupType switch
        {
            BackupType.Full => "DATABASE",
            BackupType.Differential => "DATABASE",
            BackupType.Log => "LOG",
            _ => throw new ArgumentException($"Unknown backup type: {job.BackupType}")
        };

        var sql = $"BACKUP {backupType} [{database.Name}] TO DISK = @BackupPath";

        if (job.BackupType == BackupType.Differential)
        {
            sql += " WITH DIFFERENTIAL";
        }
        else
        {
            sql += " WITH INIT";
        }

        if (job.IsCompressed)
        {
            sql += ", COMPRESSION";
        }

        sql += ", STATS = 10"; // Progress reporting
        sql += ", CHECKSUM"; // Verify backup integrity

        return sql.Replace("@BackupPath", $"'{job.BackupFilePath}'");
    }
}
