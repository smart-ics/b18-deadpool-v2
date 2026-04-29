namespace Deadpool.Core.Interfaces;

public interface IStorageInfoProvider
{
    Task<(long totalBytes, long freeBytes)> GetStorageInfoAsync(string volumePath);
    Task<bool> IsVolumeAccessibleAsync(string volumePath);
}
