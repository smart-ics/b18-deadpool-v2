namespace Deadpool.Core.Interfaces;

/// <summary>
/// Service for copying completed backup files to remote storage.
/// Implements copy-after-backup pattern: local backup completes first,
/// then copy to remote storage.
/// </summary>
public interface IBackupFileCopyService
{
    /// <summary>
    /// Copies a backup file to the configured remote storage destination.
    /// Verifies file integrity after copy (file exists, size matches).
    /// </summary>
    /// <param name="sourceFilePath">Path to the local backup file</param>
    /// <param name="databaseName">Database name (used for organizing destination path)</param>
    /// <param name="backupType">Type of backup (Full, Differential, Log)</param>
    /// <returns>Destination path where file was copied</returns>
    /// <exception cref="FileNotFoundException">Source file does not exist</exception>
    /// <exception cref="InvalidOperationException">Copy failed validation</exception>
    /// <exception cref="IOException">Network share unavailable or copy failed</exception>
    Task<string> CopyBackupFileAsync(string sourceFilePath, string databaseName, Core.Domain.Enums.BackupType backupType);
}
