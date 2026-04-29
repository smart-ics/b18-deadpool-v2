using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Deadpool.Core.Services;

/// <summary>
/// Checks for an existing Full backup and executes a bootstrap Full backup when none exists.
/// </summary>
public class BackupChainInitializationService : IBackupChainInitializationService
{
    private readonly IBackupJobRepository _jobRepository;
    private readonly BackupService _backupService;
    private readonly ILogger<BackupChainInitializationService> _logger;

    public BackupChainInitializationService(
        IBackupJobRepository jobRepository,
        BackupService backupService,
        ILogger<BackupChainInitializationService> logger)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsChainInitializedAsync(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        return await _jobRepository.HasSuccessfulFullBackupAsync(databaseName);
    }

    public async Task<bool> BootstrapAsync(string databaseName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        _logger.LogInformation(
            "Bootstrap: executing initial Full backup for database '{Database}'.", databaseName);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _backupService.ExecuteFullBackupAsync(databaseName);

            _logger.LogInformation(
                "Bootstrap: Full backup completed successfully for '{Database}'.", databaseName);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Bootstrap: cancelled for '{Database}'.", databaseName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Bootstrap: Full backup failed for '{Database}'. Differential and Log backups are blocked.",
                databaseName);
            return false;
        }
    }
}
