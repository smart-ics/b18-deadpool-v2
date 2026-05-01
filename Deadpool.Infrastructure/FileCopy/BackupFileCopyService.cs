using Microsoft.Extensions.Logging;

namespace Deadpool.Infrastructure.FileCopy;

public class BackupFileCopyService : IBackupFileCopyService
{
    private readonly ILogger<BackupFileCopyService> _logger;
    private readonly string _remoteStoragePath;
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _retryDelay;

    public BackupFileCopyService(
        ILogger<BackupFileCopyService> logger,
        string remoteStoragePath,
        int maxRetryAttempts,
        TimeSpan retryDelay)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _remoteStoragePath = remoteStoragePath ?? throw new ArgumentNullException(nameof(remoteStoragePath));
        _maxRetryAttempts = maxRetryAttempts;
        _retryDelay = retryDelay;
    }

    public async Task CopyBackupFileAsync(string sourceFilePath, string databaseName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path cannot be empty.", nameof(sourceFilePath));

        if (string.IsNullOrWhiteSpace(_remoteStoragePath))
            throw new InvalidOperationException("Remote storage path is not configured.");

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source backup file not found: {sourceFilePath}", sourceFilePath);

        Directory.CreateDirectory(_remoteStoragePath);

        var destinationFilePath = Path.Combine(_remoteStoragePath, Path.GetFileName(sourceFilePath));
        var attempts = Math.Max(1, _maxRetryAttempts);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Copying backup file for {Database}. Source: {Source}. Destination: {Destination}. Attempt {Attempt}/{Attempts}",
                    databaseName,
                    sourceFilePath,
                    destinationFilePath,
                    attempt,
                    attempts);

                await using var sourceStream = new FileStream(
                    sourceFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

                await using var destinationStream = new FileStream(
                    destinationFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                await sourceStream.CopyToAsync(destinationStream, cancellationToken);

                _logger.LogInformation(
                    "Backup file copy completed for {Database}. Destination: {Destination}",
                    databaseName,
                    destinationFilePath);

                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= attempts)
                {
                    _logger.LogError(
                        ex,
                        "Backup file copy failed for {Database}. Source: {Source}. DestinationRoot: {DestinationRoot}",
                        databaseName,
                        sourceFilePath,
                        _remoteStoragePath);
                    throw;
                }

                _logger.LogWarning(
                    ex,
                    "Backup file copy attempt {Attempt}/{Attempts} failed for {Database}. Retrying in {RetryDelay}",
                    attempt,
                    attempts,
                    databaseName,
                    _retryDelay);

                await Task.Delay(_retryDelay, cancellationToken);
            }
        }
    }
}
            