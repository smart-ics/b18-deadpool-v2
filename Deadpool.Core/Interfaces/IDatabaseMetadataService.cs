using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Interfaces;

public interface IDatabaseMetadataService
{
    Task<RecoveryModel> GetRecoveryModelAsync(string databaseName);
}
