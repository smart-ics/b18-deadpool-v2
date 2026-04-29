namespace Deadpool.Core.Interfaces;

/// <summary>
/// Abstraction for deleting backup files from storage.
/// Allows mocking for testing and supports different storage backends.
/// </summary>
public interface IBackupFileDeleter
{
    /// <summary>
    /// Deletes a backup file from storage.
    /// </summary>
    /// <param name="filePath">Full path to the backup file to delete</param>
    /// <returns>True if file was deleted successfully, false otherwise</returns>
    Task<bool> DeleteBackupFileAsync(string filePath);

    /// <summary>
    /// Checks if a backup file exists in storage.
    /// </summary>
    /// <param name="filePath">Full path to the backup file</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string filePath);
}
