using Dapper;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Data.SqlClient;

namespace Deadpool.Infrastructure.Metadata;

public class SqlServerDatabaseMetadataService : IDatabaseMetadataService
{
    private readonly string _connectionString;

    public SqlServerDatabaseMetadataService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public async Task<RecoveryModel> GetRecoveryModelAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        var query = @"
            SELECT recovery_model_desc
            FROM sys.databases
            WHERE name = @DatabaseName";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var recoveryModelDesc = await connection.QuerySingleOrDefaultAsync<string>(
            query,
            new { DatabaseName = databaseName });

        if (recoveryModelDesc == null)
            throw new InvalidOperationException($"Database '{databaseName}' not found.");

        return recoveryModelDesc switch
        {
            "SIMPLE" => RecoveryModel.Simple,
            "FULL" => RecoveryModel.Full,
            "BULK_LOGGED" => RecoveryModel.BulkLogged,
            _ => throw new InvalidOperationException($"Unknown recovery model: {recoveryModelDesc}")
        };
    }
}
