using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.Storage;

public class FileSystemStorageInfoProvider : IStorageInfoProvider
{
    private readonly ILogger<FileSystemStorageInfoProvider> _logger;

    public FileSystemStorageInfoProvider(ILogger<FileSystemStorageInfoProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<(long totalBytes, long freeBytes)> GetStorageInfoAsync(string volumePath)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
            throw new ArgumentException("Volume path cannot be empty.", nameof(volumePath));

        try
        {
            var driveInfo = GetDriveInfo(volumePath);

            if (!driveInfo.IsReady)
                throw new InvalidOperationException($"Drive not ready: {volumePath}");

            var totalBytes = driveInfo.TotalSize;
            var freeBytes = driveInfo.AvailableFreeSpace;

            _logger.LogDebug(
                "Retrieved storage info for {VolumePath}: {FreeBytes} / {TotalBytes} bytes free",
                volumePath, freeBytes, totalBytes);

            return Task.FromResult((totalBytes, freeBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get storage info for {VolumePath}", volumePath);
            throw;
        }
    }

    public Task<bool> IsVolumeAccessibleAsync(string volumePath)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
            throw new ArgumentException("Volume path cannot be empty.", nameof(volumePath));

        try
        {
            var driveInfo = GetDriveInfo(volumePath);
            var isAccessible = driveInfo.IsReady;

            _logger.LogDebug(
                "Volume {VolumePath} accessibility check: {IsAccessible}",
                volumePath, isAccessible);

            return Task.FromResult(isAccessible);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check volume accessibility for {VolumePath}", volumePath);
            return Task.FromResult(false);
        }
    }

    private DriveInfo GetDriveInfo(string volumePath)
    {
        var fullPath = Path.GetFullPath(volumePath);
        var rootPath = Path.GetPathRoot(fullPath);

        if (string.IsNullOrEmpty(rootPath))
            throw new ArgumentException($"Cannot determine root path for: {volumePath}");

        return new DriveInfo(rootPath);
    }
}
