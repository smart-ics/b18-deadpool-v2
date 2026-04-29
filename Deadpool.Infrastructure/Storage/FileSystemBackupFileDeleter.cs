using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Storage;

/// <summary>
/// File system-based backup file deleter.
/// Deletes backup files from local or network file system.
/// </summary>
public class FileSystemBackupFileDeleter : IBackupFileDeleter
{
    public async Task<bool> DeleteBackupFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            });

            // Verify deletion succeeded
            return !File.Exists(filePath);
        }
        catch
        {
            // Any IO exception means deletion failed
            return false;
        }
    }

    public Task<bool> FileExistsAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        try
        {
            bool exists = File.Exists(filePath);
            return Task.FromResult(exists);
        }
        catch
        {
            // If we can't determine existence, assume it doesn't exist
            return Task.FromResult(false);
        }
    }
}
