namespace Deadpool.Core.Interfaces;

public interface IAgentHeartbeatRepository
{
    Task UpsertHeartbeatAsync(DateTime lastSeenUtc);
    Task<DateTime?> GetLastSeenUtcAsync();
}