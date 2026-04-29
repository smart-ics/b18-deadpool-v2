using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Metadata;

// Stub implementation for testing without SQL Server.
// Always returns Full recovery model.
public sealed class StubDatabaseMetadataService : IDatabaseMetadataService
{
    public Task<RecoveryModel> GetRecoveryModelAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        // Stub: always return Full recovery model
        return Task.FromResult(RecoveryModel.Full);
    }
}
