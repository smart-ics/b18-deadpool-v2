using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.BackupExecution;
using Deadpool.Infrastructure.Metadata;
using Microsoft.Data.SqlClient;

namespace Deadpool.Agent.Infrastructure;

public static class ProductionSqlRuntimeFactory
{
    public static IBackupExecutor CreateBackupExecutor(string? connectionString, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("ProductionDatabase connection string not configured — using StubBackupExecutor");
            return new StubBackupExecutor();
        }

        return new SqlServerBackupExecutor(connectionString);
    }

    public static IDatabaseMetadataService CreateDatabaseMetadataService(string? connectionString, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("ProductionDatabase connection string not configured — using StubDatabaseMetadataService");
            return new StubDatabaseMetadataService();
        }

        return new SqlServerDatabaseMetadataService(connectionString);
    }

    public static IDatabaseConnectivityProbe CreateDatabaseConnectivityProbe(
        string? connectionString, string? databaseName, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
        {
            logger.LogWarning("ProductionDatabase connection string or database name not configured — using StubDatabaseConnectivityProbe");
            return new StubDatabaseConnectivityProbe();
        }

        return new SqlServerDatabaseConnectivityProbe(connectionString, databaseName);
    }

    public static async Task LogConnectivityCheckAsync(string? connectionString, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("ProductionDatabase connection string not configured — skipping SQL connectivity check");
            return;
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            logger.LogInformation("ProductionDatabase SQL connectivity check succeeded.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProductionDatabase SQL connectivity check failed.");
        }
    }
}
