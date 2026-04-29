using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Deadpool.Tests.Services;

public class RetentionCleanupServiceTests
{
    private readonly Mock<IBackupJobRepository> _repositoryMock;
    private readonly Mock<IBackupFileDeleter> _fileDeleterMock;
    private readonly Mock<ILogger<RetentionCleanupService>> _loggerMock;
    private readonly RetentionCleanupService _service;
    private readonly RetentionPolicy _defaultPolicy;

    public RetentionCleanupServiceTests()
    {
        _repositoryMock = new Mock<IBackupJobRepository>();
        _fileDeleterMock = new Mock<IBackupFileDeleter>();
        _loggerMock = new Mock<ILogger<RetentionCleanupService>>();
        _service = new RetentionCleanupService(_repositoryMock.Object, _fileDeleterMock.Object, _loggerMock.Object);
        _defaultPolicy = new RetentionPolicy(
            fullBackupRetention: TimeSpan.FromDays(30),
            differentialBackupRetention: TimeSpan.FromDays(14),
            logBackupRetention: TimeSpan.FromDays(7));
    }

    #region Safety Rule Tests

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldNeverDeleteLatestFullBackup()
    {
        // Arrange: Latest Full is 40 days old (expired), but should still be retained
        var now = DateTime.UtcNow;
        var latestFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_latest.bak");
        var olderFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-50), "full_old.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { latestFull, olderFull });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert
        result.RetainedBackups.Should().Contain(latestFull);
        result.DeletedBackups.Should().NotContain(latestFull);
        result.SafetyReasons.Should().Contain(r => r.Contains("Latest Full backup - ALWAYS retained"));

        // Latest Full should NEVER be deleted, even if expired
        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(latestFull.BackupFilePath),
            Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldRetainDifferentialsNeededByRetainedFull()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var fullBackup = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-10), "full.bak");
        var diff1 = CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-9), "diff1.bak");
        var diff2 = CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-8), "diff2.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, diff1, diff2 });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: All differentials should be retained because Full is retained
        result.RetainedBackups.Should().Contain(diff1);
        result.RetainedBackups.Should().Contain(diff2);
        result.DeletedBackups.Should().BeEmpty();

        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldRetainLogsNeededByRestoreChain()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var fullBackup = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-10), "full.bak");
        var log1 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-9), "log1.trn");
        var log2 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-8), "log2.trn");
        var log3 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-7), "log3.trn");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1, log2, log3 });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: All logs should be retained as part of restore chain
        result.RetainedBackups.Should().Contain(log1);
        result.RetainedBackups.Should().Contain(log2);
        result.RetainedBackups.Should().Contain(log3);
        result.DeletedBackups.Should().BeEmpty();

        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldDeleteExpiredBackupsNotNeededForChain()
    {
        // Arrange: Two Full backups, old one is expired and not latest
        var now = DateTime.UtcNow;
        var latestFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_latest.bak");
        var expiredFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_expired.bak");
        var expiredDiff = CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-39), "diff_expired.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { latestFull, expiredFull, expiredDiff });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert
        result.RetainedBackups.Should().Contain(latestFull);
        result.DeletedBackups.Should().Contain(expiredFull);
        result.DeletedBackups.Should().Contain(expiredDiff);

        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(expiredFull.BackupFilePath),
            Times.Once);
        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(expiredDiff.BackupFilePath),
            Times.Once);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldPreserveCompleteRestoreChainAcrossMultipleFullBackups()
    {
        // Arrange: Complex scenario with multiple Full backups and their chains
        var now = DateTime.UtcNow;

        // Latest Full backup chain (should all be retained)
        var latestFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_latest.bak");
        var latestDiff = CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-4), "diff_latest.bak");
        var latestLog1 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-3), "log_latest1.trn");
        var latestLog2 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-2), "log_latest2.trn");

        // Previous Full backup chain (within retention, should be retained)
        var previousFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-20), "full_previous.bak");
        var previousDiff = CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-19), "diff_previous.bak");
        var previousLog = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-18), "log_previous.trn");

        // Old Full backup chain (expired, should be deleted)
        var oldFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_old.bak");
        var oldDiff = CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-39), "diff_old.bak");
        var oldLog = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-38), "log_old.trn");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob>
            {
                latestFull, latestDiff, latestLog1, latestLog2,
                previousFull, previousDiff, previousLog,
                oldFull, oldDiff, oldLog
            });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: Latest and previous chains retained, old chain deleted
        result.RetainedBackups.Should().Contain(new[] { latestFull, latestDiff, latestLog1, latestLog2 });
        result.RetainedBackups.Should().Contain(new[] { previousFull, previousDiff, previousLog });
        result.DeletedBackups.Should().Contain(new[] { oldFull, oldDiff, oldLog });

        result.EvaluatedCount.Should().Be(10);
        result.DeletedCount.Should().Be(3);
        result.RetainedCount.Should().Be(7);
    }

    #endregion

    #region Dry Run Tests

    [Fact]
    public async Task CleanupExpiredBackupsAsync_DryRun_ShouldNotDeleteFiles()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var latestFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_latest.bak");
        var expiredFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_expired.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { latestFull, expiredFull });

        // Act: Dry run
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy, isDryRun: true);

        // Assert: Should report what would be deleted but not actually delete
        result.IsDryRun.Should().BeTrue();
        result.DeletedBackups.Should().Contain(expiredFull);
        result.RetainedBackups.Should().Contain(latestFull);

        // File deleter should NEVER be called in dry run mode
        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(It.IsAny<string>()),
            Times.Never);
        _fileDeleterMock.Verify(
            f => f.FileExistsAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_DryRun_ShouldProduceAccurateReport()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var backups = new List<BackupJob>
        {
            CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_recent.bak"),
            CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-4), "log_recent.trn"), // Part of latest Full chain
            CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_expired1.bak"),
            CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-50), "full_expired2.bak"),
            CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-39), "diff_expired.bak"),
            CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-38), "log_expired.trn")
        };

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(backups);

        // Act: Dry run
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy, isDryRun: true);

        // Assert
        result.IsDryRun.Should().BeTrue();
        result.EvaluatedCount.Should().Be(6);
        // Deleted: two expired Fulls (-40, -50) + expired Diff + expired Log (part of old chain)
        result.DeletedCount.Should().Be(4);
        // Retained: latest Full + its transaction log
        result.RetainedCount.Should().Be(2);
        result.GetSummary().Should().Contain("DRY RUN");
    }

    #endregion

    #region Deletion Failure Handling Tests

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldHandleDeletionFailuresGracefully()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var latestFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_latest.bak");
        var expiredFull1 = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_expired1.bak");
        var expiredFull2 = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-50), "full_expired2.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { latestFull, expiredFull1, expiredFull2 });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Simulate deletion failure for first expired backup
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(expiredFull1.BackupFilePath))
            .ReturnsAsync(false);

        // Success for second expired backup
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(expiredFull2.BackupFilePath))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: Should continue despite failure
        result.DeletedCount.Should().Be(1); // Only expiredFull2
        result.RetainedCount.Should().Be(2); // latestFull + expiredFull1 (failed deletion)
        result.FailedDeletionCount.Should().Be(1);
        result.HasFailures.Should().BeTrue();
        result.IsSuccessful.Should().BeFalse();
        result.DeletionFailures.Should().ContainSingle()
            .Which.Should().Contain(expiredFull1.BackupFilePath);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldHandleFileDeleterExceptions()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var latestFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_latest.bak");
        var expiredFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_expired.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { latestFull, expiredFull });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Simulate exception during deletion
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(expiredFull.BackupFilePath))
            .ThrowsAsync(new IOException("Disk error"));

        // Act - should not throw
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: Exception should be caught and treated as deletion failure
        result.DeletedCount.Should().Be(0);
        result.RetainedCount.Should().Be(2);
        result.FailedDeletionCount.Should().Be(1);
        result.HasFailures.Should().BeTrue();
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldTreatMissingFileAsDeletionSuccess()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var latestFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_latest.bak");
        var expiredFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_expired.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { latestFull, expiredFull });

        // File doesn't exist
        _fileDeleterMock.Setup(f => f.FileExistsAsync(expiredFull.BackupFilePath))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: Missing file should be treated as successful deletion
        result.DeletedCount.Should().Be(1);
        result.DeletedBackups.Should().Contain(expiredFull);
        result.FailedDeletionCount.Should().Be(0);

        // Should not attempt to delete file that doesn't exist
        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(expiredFull.BackupFilePath),
            Times.Never);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldHandleNoBackups()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob>());

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert
        result.EvaluatedCount.Should().Be(0);
        result.DeletedCount.Should().Be(0);
        result.RetainedCount.Should().Be(0);
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldHandleOnlyPendingOrFailedBackups()
    {
        // Arrange
        var pending = new BackupJob("TestDB", BackupType.Full, "full_pending.bak");
        var failed = new BackupJob("TestDB", BackupType.Full, "full_failed.bak");
        failed.MarkAsRunning();
        failed.MarkAsFailed("Backup failed");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { pending, failed });

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: Should ignore non-completed backups
        result.EvaluatedCount.Should().Be(0);
        result.DeletedCount.Should().Be(0);
        result.RetainedCount.Should().Be(0);

        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldNotDeleteIfOnlyOneFullBackupExists()
    {
        // Arrange: Only one Full backup, even if expired
        var now = DateTime.UtcNow;
        var onlyFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-50), "full_only.bak");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { onlyFull });

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: Should retain the only Full backup regardless of age
        result.RetainedBackups.Should().Contain(onlyFull);
        result.DeletedBackups.Should().BeEmpty();
        result.SafetyReasons.Should().Contain(r => r.Contains("Latest Full backup - ALWAYS retained"));

        _fileDeleterMock.Verify(
            f => f.DeleteBackupFileAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldThrow_WhenDatabaseNameEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CleanupExpiredBackupsAsync("", _defaultPolicy));
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldThrow_WhenRetentionPolicyNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.CleanupExpiredBackupsAsync("TestDB", null!));
    }

    #endregion

    #region Log Chain Edge Case Tests

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldDeleteExpiredLogsAfterNextFullBackup()
    {
        // Arrange: Logs that belong to an old Full (now superseded by new Full)
        var now = DateTime.UtcNow;
        var oldFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-40), "full_old.bak");
        var oldLog1 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-39), "log_old1.trn");
        var oldLog2 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-38), "log_old2.trn");
        var newFull = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-5), "full_new.bak");
        var newLog = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-4), "log_new.trn");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { oldFull, oldLog1, oldLog2, newFull, newLog });

        _fileDeleterMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _fileDeleterMock.Setup(f => f.DeleteBackupFileAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: Old Full and its logs should be deleted, new Full and log retained
        result.DeletedBackups.Should().Contain(new[] { oldFull, oldLog1, oldLog2 });
        result.RetainedBackups.Should().Contain(new[] { newFull, newLog });
        result.DeletedCount.Should().Be(3);
        result.RetainedCount.Should().Be(2);
    }

    [Fact]
    public async Task CleanupExpiredBackupsAsync_ShouldRetainLogsBetweenFullAndDifferential()
    {
        // Arrange: Logs between Full and Differential are part of restore chain
        var now = DateTime.UtcNow;
        var fullBackup = CreateCompletedBackup("TestDB", BackupType.Full, now.AddDays(-10), "full.bak");
        var log1 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-9), "log1.trn");
        var log2 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-8), "log2.trn");
        var diffBackup = CreateCompletedBackup("TestDB", BackupType.Differential, now.AddDays(-7), "diff.bak");
        var log3 = CreateCompletedBackup("TestDB", BackupType.TransactionLog, now.AddDays(-6), "log3.trn");

        _repositoryMock.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1, log2, diffBackup, log3 });

        // Act
        var result = await _service.CleanupExpiredBackupsAsync("TestDB", _defaultPolicy);

        // Assert: All logs in the chain should be retained
        result.RetainedBackups.Should().Contain(new[] { fullBackup, log1, log2, diffBackup, log3 });
        result.DeletedBackups.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static BackupJob CreateCompletedBackup(
        string databaseName,
        BackupType backupType,
        DateTime startTime,
        string filePath)
    {
        var backup = new BackupJob(databaseName, backupType, filePath);

        // Use reflection to set StartTime since it's read-only
        var startTimeField = typeof(BackupJob).GetField("<StartTime>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        startTimeField?.SetValue(backup, startTime);

        backup.MarkAsRunning();
        backup.MarkAsCompleted(100000); // 100KB file size

        return backup;
    }

    #endregion
}
