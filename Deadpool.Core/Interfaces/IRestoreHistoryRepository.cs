using Deadpool.Core.Domain.Entities;

namespace Deadpool.Core.Interfaces;

public interface IRestoreHistoryRepository
{
    Task SaveAsync(RestoreHistoryRecord record);
    Task<IReadOnlyList<RestoreHistoryRecord>> GetRecentAsync(int limit);
}
