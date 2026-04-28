using Deadpool.Core.Domain.Common;

namespace Deadpool.Core.Domain.Entities;

/// <summary>
/// Represents a SQL Server instance being monitored
/// </summary>
public class SqlServerInstance : Entity
{
    public string ServerName { get; private set; }
    public string? InstanceName { get; private set; }
    public int Port { get; private set; }
    public string ConnectionString { get; private set; }
    public bool IsActive { get; private set; }
    public string? Description { get; private set; }
    public DateTime? LastContactedAt { get; private set; }
    public string? Version { get; private set; }
    public string? Edition { get; private set; }

    private readonly List<Database> _databases = new();
    public IReadOnlyCollection<Database> Databases => _databases.AsReadOnly();

    private SqlServerInstance() : base()
    {
        ServerName = string.Empty;
        ConnectionString = string.Empty;
    }

    public SqlServerInstance(
        string serverName, 
        string? instanceName, 
        int port, 
        string connectionString,
        string? description = null) : base()
    {
        ServerName = serverName ?? throw new ArgumentNullException(nameof(serverName));
        InstanceName = instanceName;
        Port = port > 0 ? port : 1433;
        ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Description = description;
        IsActive = true;
    }

    public void UpdateConnectionInfo(string connectionString, int port)
    {
        ConnectionString = connectionString;
        Port = port;
    }

    public void UpdateServerInfo(string? version, string? edition)
    {
        Version = version;
        Edition = edition;
        LastContactedAt = DateTime.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void AddDatabase(Database database)
    {
        if (!_databases.Any(d => d.Name == database.Name))
        {
            _databases.Add(database);
        }
    }

    public string GetFullServerName()
    {
        return string.IsNullOrEmpty(InstanceName) 
            ? ServerName 
            : $"{ServerName}\\{InstanceName}";
    }
}
