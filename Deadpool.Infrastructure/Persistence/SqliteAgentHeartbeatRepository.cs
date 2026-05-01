using Dapper;
using Deadpool.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence;

public class SqliteAgentHeartbeatRepository : IAgentHeartbeatRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteAgentHeartbeatRepository> _logger;

    public SqliteAgentHeartbeatRepository(string databasePath, ILogger<SqliteAgentHeartbeatRepository> logger)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be empty.", nameof(databasePath));

        _connectionString = $"Data Source={databasePath};";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        EnsureTableCreated();
    }

    private void EnsureTableCreated()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            CREATE TABLE IF NOT EXISTS AgentHeartbeat (
                Id INTEGER PRIMARY KEY,
                LastSeenUtc TEXT NOT NULL
            );
        ";

        connection.Execute(sql);
        _logger.LogInformation("SQLite agent heartbeat repository initialized at {ConnectionString}", _connectionString);
    }

    public async Task UpsertHeartbeatAsync(DateTime lastSeenUtc)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO AgentHeartbeat (Id, LastSeenUtc)
            VALUES (1, @LastSeenUtc)
            ON CONFLICT(Id) DO UPDATE SET LastSeenUtc = excluded.LastSeenUtc;
        ";

        await connection.ExecuteAsync(sql, new { LastSeenUtc = lastSeenUtc.ToString("O") });
    }

    public async Task<DateTime?> GetLastSeenUtcAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT LastSeenUtc FROM AgentHeartbeat WHERE Id = 1 LIMIT 1;";
        var value = await connection.QuerySingleOrDefaultAsync<string?>(sql);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}