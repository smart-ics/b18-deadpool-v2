using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Interfaces;

public interface IDatabasePulseRepository
{
    Task CreateAsync(DatabasePulseRecord record);
    Task<DatabasePulseRecord?> GetLatestAsync();
    void CleanupOldRecords(TimeSpan retention);
}
