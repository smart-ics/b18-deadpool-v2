using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.FileCopy;

/// <summary>
/// Implements backup file copying to remote storage with retry logic and integrity validation.
/// Copy pattern: Local backup completes first, then copy to remote storage.
/// Copy failure never endangers the original local backup.
/// </summary>
public sealed class BackupFileCopyService : IBackupFileCopyService
{
    private readonly ILogger<BackupFileCopyService> _logger;
    private readonly string _remoteStoragePath;
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _retryDelay;

    public BackupFileCopyService(
        ILogger<BackupFileCopyService> logger,
        string remoteStoragePath,
        int maxRetryAttempts = 3,
        TimeSpan? retryDelay = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(remoteStoragePath))
            throw new ArgumentException("Remote storage path cannot be empty.", nameof(remoteStoragePath));

        _remoteStoragePath = remoteStoragePath;
        _maxRetryAttempts = maxRetryAttempts;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(5);
    }

    public async Task<string> CopyBackupFileAsync(
        string sourceFilePath,
        string databaseName,
        BackupType backupType)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path cannot be empty.", nameof(sourceFilePath));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        // Validate source file exists
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source backup file not found: {sourceFilePath}");

        var sourceFileInfo = new FileInfo(sourceFilePath);
        var sourceSize = sourceFileInfo.Length;

        // Reject zero-byte files (invalid backups)
        if (sourceSize == 0)
            throw new InvalidOperationException(
                $"Source backup file is empty (0 bytes): {sourceFilePath}. " +
                "This is not a valid SQL Server backup file.");

        // Build destination path
        var destinationPath = BuildDestinationPath(sourceFilePath, databaseName);

        _logger.LogInformation(
            "Starting backup file copy: {Source} → {Destination} (size: {Size} bytes)",
            sourceFilePath, destinationPath, sourceSize);

        // Copy with retry logic
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetryAttempts)
        {
            attempt++;

            try
            {
                await CopyFileWithValidationAsync(sourceFilePath, destinationPath, sourceSize);

                _logger.LogInformation(
                    "Backup file copied successfully: {Destination} (attempt {Attempt}/{Max})",
                    destinationPath, attempt, _maxRetryAttempts);

                return destinationPath;
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < _maxRetryAttempts)
            {
                lastException = ex;

                _logger.LogWarning(ex,
                    "Transient error copying backup file (attempt {Attempt}/{Max}). " +
                    "Retrying in {Delay}...",
                    attempt, _maxRetryAttempts, _retryDelay);

                await Task.Delay(_retryDelay);
            }
            catch (InvalidOperationException ex) when (attempt < _maxRetryAttempts)
            {
                // Treat validation failures as transient (may be caused by AV scan, file locks, etc.)
                lastException = ex;

                _logger.LogWarning(ex,
                    "Validation error copying backup file (attempt {Attempt}/{Max}). " +
                    "Retrying in {Delay}...",
                    attempt, _maxRetryAttempts, _retryDelay);

                await Task.Delay(_retryDelay);
            }
        }

        // All retries exhausted
        var errorMessage = $"Failed to copy backup file after {_maxRetryAttempts} attempts: {lastException?.Message}";
        _logger.LogError(lastException, errorMessage);
        throw new IOException(errorMessage, lastException);
    }

    private async Task CopyFileWithValidationAsync(string sourcePath, string destinationPath, long expectedSize)
    {
        // Ensure destination directory exists
        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
        {
            try
            {
                Directory.CreateDirectory(destinationDir);
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"Failed to create destination directory: {destinationDir}",
                    ex);
            }
        }

        // Copy file (async to avoid blocking)
        try
        {
            await using var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920, // 80 KB buffer
                useAsync: true);

            await using var destStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await sourceStream.CopyToAsync(destStream);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to copy file: {ex.Message}", ex);
        }

        // Validate copy integrity
        ValidateCopiedFile(destinationPath, expectedSize);
    }

    private void ValidateCopiedFile(string destinationPath, long expectedSize)
    {
        // Check file exists
        if (!File.Exists(destinationPath))
        {
            throw new InvalidOperationException(
                $"Copy validation failed: Destination file does not exist: {destinationPath}");
        }

        // Check file size matches
        var destFileInfo = new FileInfo(destinationPath);
        var actualSize = destFileInfo.Length;

        if (actualSize != expectedSize)
        {
            // Delete incomplete file
            try
            {
                File.Delete(destinationPath);
            }
            catch
            {
                // Best effort cleanup
            }

            throw new InvalidOperationException(
                $"Copy validation failed: File size mismatch. " +
                $"Expected {expectedSize} bytes, got {actualSize} bytes. " +
                $"Destination: {destinationPath}");
        }

        // Verify file is readable (read first 1KB)
        VerifyFileReadable(destinationPath);

        // Validate SQL Server backup header
        ValidateSqlServerBackupHeader(destinationPath);
    }

    private void VerifyFileReadable(string filePath)
    {
        try
        {
            using var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            var buffer = new byte[1024]; // Read first 1KB
            var bytesRead = fs.Read(buffer, 0, buffer.Length);

            if (bytesRead == 0)
            {
                throw new InvalidOperationException(
                    $"Copy validation failed: File is not readable (0 bytes read): {filePath}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Copy validation failed: Unable to read destination file: {filePath}",
                ex);
        }
    }

    private void ValidateSqlServerBackupHeader(string filePath)
    {
        try
        {
            using var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            // Read first 16 bytes for signature
            var header = new byte[16];
            var bytesRead = fs.Read(header, 0, header.Length);

            if (bytesRead < 4)
            {
                throw new InvalidOperationException(
                    $"Copy validation failed: File too small to contain SQL Server backup header: {filePath}");
            }

            // Check for SQL Server backup signatures
            var signature = System.Text.Encoding.ASCII.GetString(header, 0, 4);

            if (signature != "TAPE" && signature != "MTF ")
            {
                throw new InvalidOperationException(
                    $"Copy validation failed: Invalid SQL Server backup signature. " +
                    $"Expected 'TAPE' or 'MTF ', got '{signature}': {filePath}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Copy validation failed: Unable to validate backup header: {filePath}",
                ex);
        }
    }

    private string BuildDestinationPath(string sourceFilePath, string databaseName)
    {
        var fileName = Path.GetFileName(sourceFilePath);

        // Organize by database name subdirectory
        var destinationPath = Path.Combine(_remoteStoragePath, databaseName, fileName);

        return destinationPath;
    }

    private static bool IsTransientError(Exception ex)
    {
        // Transient errors that are worth retrying
        return ex is IOException ||
               ex is UnauthorizedAccessException ||
               ex is TimeoutException;
    }
}
