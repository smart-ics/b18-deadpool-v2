using System.Text.RegularExpressions;
using Dapper;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Data.SqlClient;

namespace Deadpool.Infrastructure.BackupExecution;

public class SqlServerBackupExecutor : IBackupExecutor
{
    private readonly string _connectionString;

    public SqlServerBackupExecutor(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public async Task ExecuteFullBackupAsync(string databaseName, string backupFilePath)
    {
        await ExecuteBackupCommandAsync(databaseName, backupFilePath, GenerateFullBackupCommand);
    }

    public async Task ExecuteDifferentialBackupAsync(string databaseName, string backupFilePath)
    {
        await ExecuteBackupCommandAsync(databaseName, backupFilePath, GenerateDifferentialBackupCommand);
    }

    public async Task ExecuteTransactionLogBackupAsync(string databaseName, string backupFilePath)
    {
        await ExecuteBackupCommandAsync(databaseName, backupFilePath, GenerateTransactionLogBackupCommand);
    }

    public async Task<bool> VerifyBackupFileAsync(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        if (!File.Exists(backupFilePath))
            return false;

        var verifyCommand = "RESTORE VERIFYONLY FROM DISK = @BackupFilePath";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            verifyCommand,
            new { BackupFilePath = backupFilePath },
            commandTimeout: 300);

        return true;
    }

    // Template method: Common backup command execution pattern
    private async Task ExecuteBackupCommandAsync(
        string databaseName,
        string backupFilePath,
        Func<string, string> commandGenerator)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        ValidateDatabaseName(databaseName);

        var backupCommand = commandGenerator(databaseName);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var commandTimeout = 3600; // 1 hour timeout for large backups
        await connection.ExecuteAsync(
            backupCommand,
            new { BackupFilePath = backupFilePath },
            commandTimeout: commandTimeout);
    }

    private string GenerateFullBackupCommand(string databaseName)
    {
        // NOTE: Database name cannot be parameterized in BACKUP DATABASE command.
        // ValidateDatabaseName() ensures safe identifier before this method is called.
        // 
        // FORMAT option intentionally omitted:
        // - INIT overwrites existing backup file, which is appropriate for scheduled full backups
        // - FORMAT would create new media set, unnecessary for local disk backups
        // - Keeping simpler backup options reduces potential failure points
        //
        // Required SQL Server permissions:
        // - BACKUP DATABASE permission on the target database
        // - Or membership in db_backupoperator, db_owner, or sysadmin roles
        // - Write permission on backup file location
        return $@"
            BACKUP DATABASE [{databaseName}]
            TO DISK = @BackupFilePath
            WITH 
                INIT,
                COMPRESSION,
                CHECKSUM,
                STATS = 10";
    }

    private string GenerateDifferentialBackupCommand(string databaseName)
    {
        // NOTE: Differential backup captures changes since last FULL backup.
        // 
        // DIFFERENTIAL keyword:
        // - Backs up only data that has changed since the last full backup
        // - Depends on valid full backup base
        // - Does not break transaction log chain
        //
        // INIT option:
        // - Overwrites existing differential backup file
        // - Appropriate for scheduled differential backups
        //
        // Required SQL Server permissions:
        // - BACKUP DATABASE permission on the target database
        // - Or membership in db_backupoperator, db_owner, or sysadmin roles
        // - Write permission on backup file location
        return $@"
            BACKUP DATABASE [{databaseName}]
            TO DISK = @BackupFilePath
            WITH 
                DIFFERENTIAL,
                INIT,
                COMPRESSION,
                CHECKSUM,
                STATS = 10";
    }

    private string GenerateTransactionLogBackupCommand(string databaseName)
    {
        // NOTE: Transaction log backup requires FULL or BULK_LOGGED recovery model.
        // 
        // BACKUP LOG command:
        // - Backs up transaction log since last log backup (or last full backup)
        // - Truncates inactive portion of transaction log after backup
        // - Maintains log chain continuity (critical for point-in-time recovery)
        // - Must not break log chain
        //
        // INIT option:
        // - Overwrites existing log backup file
        // - Appropriate for scheduled log backups (each log backup is independent file)
        //
        // Recovery model requirements:
        // - FULL: Supports full point-in-time recovery
        // - BULK_LOGGED: Supports bulk operations with minimal logging
        // - SIMPLE: Transaction log backups NOT supported (log auto-truncates)
        //
        // Required SQL Server permissions:
        // - BACKUP LOG permission on the target database
        // - Or membership in db_backupoperator, db_owner, or sysadmin roles
        // - Write permission on backup file location
        return $@"
            BACKUP LOG [{databaseName}]
            TO DISK = @BackupFilePath
            WITH 
                INIT,
                COMPRESSION,
                CHECKSUM,
                STATS = 10";
    }

    private void ValidateDatabaseName(string databaseName)
    {
        // SQL Server database name rules:
        // - 1 to 128 characters
        // - First character: letter, underscore, @, or #
        // - Subsequent: letters, digits, @, $, #, or underscore
        // - Cannot be reserved words (not checked here for simplicity)
        // See: https://docs.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers

        if (databaseName.Length > 128)
            throw new ArgumentException($"Database name exceeds 128 characters: {databaseName}", nameof(databaseName));

        if (!Regex.IsMatch(databaseName, @"^[a-zA-Z_@#][a-zA-Z0-9_@#$]*$"))
            throw new ArgumentException(
                $"Invalid database name format: {databaseName}. " +
                "Must start with letter, underscore, @, or #. " +
                "Subsequent characters can be letters, digits, @, $, #, or underscore.",
                nameof(databaseName));
    }

    public async Task<BackupLSNMetadata?> GetBackupLSNMetadataAsync(string databaseName, string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Query msdb.dbo.backupset for LSN metadata
            // Match by database name and physical_device_name from backupmediafamily
            // Order by backup_finish_date to get the most recent backup for this file
            var sql = @"
                SELECT TOP 1
                    bs.first_lsn,
                    bs.last_lsn,
                    bs.database_backup_lsn,
                    bs.checkpoint_lsn
                FROM msdb.dbo.backupset bs
                INNER JOIN msdb.dbo.backupmediafamily bmf ON bs.media_set_id = bmf.media_set_id
                WHERE bs.database_name = @DatabaseName
                  AND bmf.physical_device_name = @BackupFilePath
                ORDER BY bs.backup_finish_date DESC";

            var result = await connection.QuerySingleOrDefaultAsync<BackupLSNMetadataDto>(
                sql,
                new { DatabaseName = databaseName, BackupFilePath = backupFilePath },
                commandTimeout: 30);

            if (result == null)
                return null;

            return new BackupLSNMetadata(
                result.first_lsn,
                result.last_lsn,
                result.database_backup_lsn,
                result.checkpoint_lsn);
        }
        catch
        {
            // LSN metadata capture failure is non-fatal
            // Conservative: Return null to signal uncertainty
            return null;
        }
    }

    private class BackupLSNMetadataDto
    {
        public decimal? first_lsn { get; set; }
        public decimal? last_lsn { get; set; }
        public decimal? database_backup_lsn { get; set; }
        public decimal? checkpoint_lsn { get; set; }
    }
}
