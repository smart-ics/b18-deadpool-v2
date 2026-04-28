using Dapper;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Persistence.Repositories;

public class SqlServerInstanceRepository : ISqlServerInstanceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerInstanceRepository> _logger;

    public SqlServerInstanceRepository(IConfiguration configuration, ILogger<SqlServerInstanceRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DeadpoolDb") 
            ?? throw new InvalidOperationException("DeadpoolDb connection string not configured");
        _logger = logger;
    }

    public async Task<SqlServerInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, ServerName, InstanceName, Port, ConnectionString, IsActive, 
                   Description, LastContactedAt, Version, Edition, 
                   CreatedAt, ModifiedAt, CreatedBy, ModifiedBy
            FROM SqlServerInstances
            WHERE Id = @Id";

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QuerySingleOrDefaultAsync<SqlServerInstanceDto>(sql, new { Id = id });
        return result?.ToEntity();
    }

    public async Task<SqlServerInstance?> GetByServerNameAsync(string serverName, string? instanceName = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, ServerName, InstanceName, Port, ConnectionString, IsActive, 
                   Description, LastContactedAt, Version, Edition, 
                   CreatedAt, ModifiedAt, CreatedBy, ModifiedBy
            FROM SqlServerInstances
            WHERE ServerName = @ServerName 
            AND (@InstanceName IS NULL OR InstanceName = @InstanceName)";

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QuerySingleOrDefaultAsync<SqlServerInstanceDto>(sql, new { ServerName = serverName, InstanceName = instanceName });
        return result?.ToEntity();
    }

    public async Task<IEnumerable<SqlServerInstance>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, ServerName, InstanceName, Port, ConnectionString, IsActive, 
                   Description, LastContactedAt, Version, Edition, 
                   CreatedAt, ModifiedAt, CreatedBy, ModifiedBy
            FROM SqlServerInstances
            WHERE IsActive = 1
            ORDER BY ServerName, InstanceName";

        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<SqlServerInstanceDto>(sql);
        return results.Select(dto => dto.ToEntity());
    }

    public async Task<IEnumerable<SqlServerInstance>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, ServerName, InstanceName, Port, ConnectionString, IsActive, 
                   Description, LastContactedAt, Version, Edition, 
                   CreatedAt, ModifiedAt, CreatedBy, ModifiedBy
            FROM SqlServerInstances
            ORDER BY ServerName, InstanceName";

        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<SqlServerInstanceDto>(sql);
        return results.Select(dto => dto.ToEntity());
    }

    public async Task AddAsync(SqlServerInstance instance, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO SqlServerInstances 
            (Id, ServerName, InstanceName, Port, ConnectionString, IsActive, Description, 
             LastContactedAt, Version, Edition, CreatedAt, CreatedBy)
            VALUES 
            (@Id, @ServerName, @InstanceName, @Port, @ConnectionString, @IsActive, @Description, 
             @LastContactedAt, @Version, @Edition, @CreatedAt, @CreatedBy)";

        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, SqlServerInstanceDto.FromEntity(instance));
        _logger.LogInformation("Added SQL Server instance: {ServerName}", instance.GetFullServerName());
    }

    public async Task UpdateAsync(SqlServerInstance instance, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE SqlServerInstances
            SET ServerName = @ServerName,
                InstanceName = @InstanceName,
                Port = @Port,
                ConnectionString = @ConnectionString,
                IsActive = @IsActive,
                Description = @Description,
                LastContactedAt = @LastContactedAt,
                Version = @Version,
                Edition = @Edition,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE Id = @Id";

        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, SqlServerInstanceDto.FromEntity(instance));
        _logger.LogInformation("Updated SQL Server instance: {ServerName}", instance.GetFullServerName());
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM SqlServerInstances WHERE Id = @Id";
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { Id = id });
        _logger.LogInformation("Deleted SQL Server instance: {Id}", id);
    }

    private class SqlServerInstanceDto
    {
        public Guid Id { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string? InstanceName { get; set; }
        public int Port { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public DateTime? LastContactedAt { get; set; }
        public string? Version { get; set; }
        public string? Edition { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? ModifiedBy { get; set; }

        public SqlServerInstance ToEntity()
        {
            var instance = new SqlServerInstance(ServerName, InstanceName, Port, ConnectionString, Description);
            typeof(SqlServerInstance).GetProperty(nameof(SqlServerInstance.Id))!.SetValue(instance, Id);
            instance.UpdateServerInfo(Version, Edition);
            if (!IsActive) instance.Deactivate();
            return instance;
        }

        public static SqlServerInstanceDto FromEntity(SqlServerInstance entity)
        {
            return new SqlServerInstanceDto
            {
                Id = entity.Id,
                ServerName = entity.ServerName,
                InstanceName = entity.InstanceName,
                Port = entity.Port,
                ConnectionString = entity.ConnectionString,
                IsActive = entity.IsActive,
                Description = entity.Description,
                LastContactedAt = entity.LastContactedAt,
                Version = entity.Version,
                Edition = entity.Edition,
                CreatedAt = entity.CreatedAt,
                ModifiedAt = entity.ModifiedAt,
                CreatedBy = entity.CreatedBy,
                ModifiedBy = entity.ModifiedBy
            };
        }
    }
}
