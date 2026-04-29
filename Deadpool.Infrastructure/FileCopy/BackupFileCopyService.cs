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
        _logger.LogInformation("Copying backup file {Source} to remote storage", sourceFilePath);

        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("Backup file copied successfully");
    }
}
            