using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Interfaces;

public interface IStorageMonitoringService
{
    Task<StorageHealthCheck> CheckStorageHealthAsync(string volumePath);
    Task<StorageHealthCheck> CheckStorageHealthAsync(string volumePath, string databaseName, BackupType nextBackupType);
}
