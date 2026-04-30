using Deadpool.Core.Interfaces;
using Microsoft.Data.SqlClient;

namespace Deadpool.Infrastructure.Metadata;

public class SqlServerDatabaseConnectivityProbe : IDatabaseConnectivityProbe
{
    private readonly string _connectionString;
    private readonly string _expectedDatabaseName;
    private const int ProbeTimeoutSeconds = 5;

    public SqlServerDatabaseConnectivityProbe(string connectionString, string expectedDatabaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(expectedDatabaseName))
            throw new ArgumentException("Expected database name cannot be empty.", nameof(expectedDatabaseName));

        var connectionBuilder = new SqlConnectionStringBuilder(connectionString);
        if (connectionBuilder.ConnectTimeout <= 0 || connectionBuilder.ConnectTimeout > ProbeTimeoutSeconds)
        {
            connectionBuilder.ConnectTimeout = ProbeTimeoutSeconds;
        }

        _connectionString = connectionBuilder.ConnectionString;
        _expectedDatabaseName = expectedDatabaseName;
    }

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = ProbeTimeoutSeconds;
        _ = await command.ExecuteScalarAsync(cancellationToken);

        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText = "SELECT DB_NAME()";
        dbCommand.CommandTimeout = ProbeTimeoutSeconds;
        var actualDatabase = (await dbCommand.ExecuteScalarAsync(cancellationToken))?.ToString();

        if (!string.Equals(actualDatabase, _expectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Connected database mismatch. Expected '{_expectedDatabaseName}', actual '{actualDatabase ?? "Unknown"}'.");
        }

        await using var stateCommand = connection.CreateCommand();
        stateCommand.CommandText = @"
            SELECT state_desc
            FROM sys.databases
            WHERE name = DB_NAME()";
        stateCommand.CommandTimeout = ProbeTimeoutSeconds;
        var state = (await stateCommand.ExecuteScalarAsync(cancellationToken))?.ToString();

        if (!string.Equals(state, "ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database '{_expectedDatabaseName}' is not ONLINE. Current state: '{state ?? "Unknown"}'.");
        }
    }
}
