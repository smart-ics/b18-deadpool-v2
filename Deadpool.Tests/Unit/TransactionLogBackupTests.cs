using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;

namespace Deadpool.Tests.Unit;

public class TransactionLogBackupTests
{
    private readonly Mock<IBackupExecutor> _mockBackupExecutor;
    private readonly Mock<IBackupJobRepository> _mockBackupJobRepository;
    private readonly Mock<IDatabaseMetadataService> _mockMetadataService;
    private readonly BackupFilePathService _filePathService;
    private readonly BackupService _backupService;

    public TransactionLogBackupTests()
    {
        _mockBackupExecutor = new Mock<IBackupExecutor>();
        _mockBackupJobRepository = new Mock<IBackupJobRepository>();
        _mockMetadataService = new Mock<IDatabaseMetadataService>();
        _filePathService = new BackupFilePathService(@"C:\Backups");

        _backupService = new BackupService(
            _mockBackupExecutor.Object,
            _mockBackupJobRepository.Object,
            _filePathService,
            _mockMetadataService.Object);
    }

    [Fact]
    public async Task ExecuteTransactionLogBackupAsync_ShouldThrowInvalidOperationException_WhenRecoveryModelIsSimple()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        _mockMetadataService
            .Setup(x => x.GetRecoveryModelAsync(databaseName))
            .ReturnsAsync(RecoveryModel.Simple);

        // Act
        var act = async () => await _backupService.ExecuteTransactionLogBackupAsync(databaseName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SIMPLE recovery model*");
    }

    [Fact]
    public async Task ExecuteTransactionLogBackupAsync_ShouldThrowInvalidOperationException_WhenNoFullBackupExists()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        _mockMetadataService
            .Setup(x => x.GetRecoveryModelAsync(databaseName))
            .ReturnsAsync(RecoveryModel.Full);

        _mockBackupJobRepository
            .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
            .ReturnsAsync(false);

        // Act
        var act = async () => await _backupService.ExecuteTransactionLogBackupAsync(databaseName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No successful full backup found*");
    }

    [Fact]
    public async Task ExecuteTransactionLogBackupAsync_ShouldExecuteBackup_WhenPrerequisitesMet_FullRecoveryModel()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.trn");
        File.WriteAllText(tempFile, "test log backup");

        try
        {
            _mockMetadataService
                .Setup(x => x.GetRecoveryModelAsync(databaseName))
                .ReturnsAsync(RecoveryModel.Full);

            _mockBackupJobRepository
                .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
                .ReturnsAsync(true);

            _mockBackupExecutor
                .Setup(x => x.ExecuteTransactionLogBackupAsync(databaseName, It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(tempFile, path, true);
                })
                .Returns(Task.CompletedTask);

            _mockBackupJobRepository
                .Setup(x => x.CreateAsync(It.IsAny<BackupJob>()))
                .Returns(Task.CompletedTask);

            _mockBackupJobRepository
                .Setup(x => x.UpdateAsync(It.IsAny<BackupJob>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _backupService.ExecuteTransactionLogBackupAsync(databaseName);

            // Assert
            result.Should().NotBeNull();
            result.BackupType.Should().Be(BackupType.TransactionLog);
            result.Status.Should().Be(BackupStatus.Completed);
            result.DatabaseName.Should().Be(databaseName);

            _mockMetadataService.Verify(x => x.GetRecoveryModelAsync(databaseName), Times.Once);
            _mockBackupJobRepository.Verify(x => x.HasSuccessfulFullBackupAsync(databaseName), Times.Once);
            _mockBackupExecutor.Verify(x => x.ExecuteTransactionLogBackupAsync(databaseName, It.IsAny<string>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteTransactionLogBackupAsync_ShouldExecuteBackup_WhenPrerequisitesMet_BulkLoggedRecoveryModel()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.trn");
        File.WriteAllText(tempFile, "test log backup");

        try
        {
            _mockMetadataService
                .Setup(x => x.GetRecoveryModelAsync(databaseName))
                .ReturnsAsync(RecoveryModel.BulkLogged);

            _mockBackupJobRepository
                .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
                .ReturnsAsync(true);

            _mockBackupExecutor
                .Setup(x => x.ExecuteTransactionLogBackupAsync(databaseName, It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(tempFile, path, true);
                })
                .Returns(Task.CompletedTask);

            _mockBackupJobRepository
                .Setup(x => x.CreateAsync(It.IsAny<BackupJob>()))
                .Returns(Task.CompletedTask);

            _mockBackupJobRepository
                .Setup(x => x.UpdateAsync(It.IsAny<BackupJob>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _backupService.ExecuteTransactionLogBackupAsync(databaseName);

            // Assert
            result.Should().NotBeNull();
            result.BackupType.Should().Be(BackupType.TransactionLog);
            result.Status.Should().Be(BackupStatus.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteTransactionLogBackupAsync_ShouldPersistJobBeforeExecution()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.trn");
        File.WriteAllText(tempFile, "test backup");

        try
        {
            var callSequence = new List<string>();

            _mockMetadataService
                .Setup(x => x.GetRecoveryModelAsync(databaseName))
                .ReturnsAsync(RecoveryModel.Full);

            _mockBackupJobRepository
                .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
                .ReturnsAsync(true);

            _mockBackupJobRepository
                .Setup(x => x.CreateAsync(It.IsAny<BackupJob>()))
                .Callback(() => callSequence.Add("CreateAsync"))
                .Returns(Task.CompletedTask);

            _mockBackupJobRepository
                .Setup(x => x.UpdateAsync(It.IsAny<BackupJob>()))
                .Callback(() => callSequence.Add("UpdateAsync"))
                .Returns(Task.CompletedTask);

            _mockBackupExecutor
                .Setup(x => x.ExecuteTransactionLogBackupAsync(databaseName, It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    callSequence.Add("ExecuteBackup");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(tempFile, path, true);
                })
                .Returns(Task.CompletedTask);

            // Act
            await _backupService.ExecuteTransactionLogBackupAsync(databaseName);

            // Assert
            callSequence[0].Should().Be("CreateAsync", "job should be persisted first");
            callSequence[1].Should().Be("UpdateAsync", "job should be marked as running");
            callSequence[2].Should().Be("ExecuteBackup", "backup should execute after persistence");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteTransactionLogBackupAsync_ShouldMarkJobAsFailed_WhenBackupFails()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        var errorMessage = "Log file full";

        _mockMetadataService
            .Setup(x => x.GetRecoveryModelAsync(databaseName))
            .ReturnsAsync(RecoveryModel.Full);

        _mockBackupJobRepository
            .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
            .ReturnsAsync(true);

        _mockBackupExecutor
            .Setup(x => x.ExecuteTransactionLogBackupAsync(databaseName, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        _mockBackupJobRepository
            .Setup(x => x.CreateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);

        _mockBackupJobRepository
            .Setup(x => x.UpdateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);

        // Act
        var act = async () => await _backupService.ExecuteTransactionLogBackupAsync(databaseName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        _mockBackupJobRepository.Verify(
            x => x.UpdateAsync(It.Is<BackupJob>(j => j.Status == BackupStatus.Failed)),
            Times.Exactly(2),
            "Failed job should be persisted (once for Running, once for Failed)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteTransactionLogBackupAsync_ShouldThrowArgumentException_WhenDatabaseNameEmpty(string databaseName)
    {
        // Act
        var act = async () => await _backupService.ExecuteTransactionLogBackupAsync(databaseName);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Database name cannot be empty.*");
    }

    [Fact]
    public void TransactionLogBackupFilePath_ShouldContainLOGInName()
    {
        // Arrange
        var service = new BackupFilePathService(@"C:\Backups");
        var databaseName = "MyHospitalDB";

        // Act
        var filePath = service.GenerateBackupFilePath(databaseName, BackupType.TransactionLog);

        // Assert
        filePath.Should().Contain("_LOG_");
        filePath.Should().EndWith(".trn");
        Path.GetFileName(filePath).Should().MatchRegex(@"^MyHospitalDB_LOG_\d{8}_\d{4}\.trn$");
    }
}
