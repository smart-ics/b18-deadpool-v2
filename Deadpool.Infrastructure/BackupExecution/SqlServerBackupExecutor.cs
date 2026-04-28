using System.Text.RegularExpressions;
using Dapper;
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
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));

        ValidateDatabaseName(databaseName);

        var backupCommand = GenerateFullBackupCommand(databaseName);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var commandTimeout = 3600; // 1 hour timeout for large backups
        await connection.ExecuteAsync(
            backupCommand,
            new { BackupFilePath = backupFilePath },
            commandTimeout: commandTimeout);
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
}
