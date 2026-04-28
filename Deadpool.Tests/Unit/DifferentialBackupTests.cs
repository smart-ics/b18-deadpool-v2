using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;

namespace Deadpool.Tests.Unit;

public class DifferentialBackupTests
{
    private readonly Mock<IBackupExecutor> _mockBackupExecutor;
    private readonly Mock<IBackupJobRepository> _mockBackupJobRepository;
    private readonly BackupFilePathService _filePathService;
    private readonly BackupService _backupService;

    public DifferentialBackupTests()
    {
        _mockBackupExecutor = new Mock<IBackupExecutor>();
        _mockBackupJobRepository = new Mock<IBackupJobRepository>();
        _filePathService = new BackupFilePathService(@"C:\Backups");

        _backupService = new BackupService(
            _mockBackupExecutor.Object,
            _mockBackupJobRepository.Object,
            _filePathService);
    }

    [Fact]
    public async Task ExecuteDifferentialBackupAsync_ShouldThrowInvalidOperationException_WhenNoFullBackupExists()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        _mockBackupJobRepository
            .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
            .ReturnsAsync(false);

        // Act
        var act = async () => await _backupService.ExecuteDifferentialBackupAsync(databaseName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No successful full backup found*");
    }

    [Fact]
    public async Task ExecuteDifferentialBackupAsync_ShouldExecuteBackup_WhenFullBackupExists()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.bak");
        File.WriteAllText(tempFile, "test differential backup");

        try
        {
            _mockBackupJobRepository
                .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
                .ReturnsAsync(true);

            _mockBackupExecutor
                .Setup(x => x.ExecuteDifferentialBackupAsync(databaseName, It.IsAny<string>()))
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
            var result = await _backupService.ExecuteDifferentialBackupAsync(databaseName);

            // Assert
            result.Should().NotBeNull();
            result.BackupType.Should().Be(BackupType.Differential);
            result.Status.Should().Be(BackupStatus.Completed);
            result.DatabaseName.Should().Be(databaseName);

            _mockBackupJobRepository.Verify(x => x.HasSuccessfulFullBackupAsync(databaseName), Times.Once);
            _mockBackupExecutor.Verify(x => x.ExecuteDifferentialBackupAsync(databaseName, It.IsAny<string>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteDifferentialBackupAsync_ShouldPersistJobBeforeExecution()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.bak");
        File.WriteAllText(tempFile, "test backup");

        try
        {
            var callSequence = new List<string>();

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
                .Setup(x => x.ExecuteDifferentialBackupAsync(databaseName, It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    callSequence.Add("ExecuteBackup");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(tempFile, path, true);
                })
                .Returns(Task.CompletedTask);

            // Act
            await _backupService.ExecuteDifferentialBackupAsync(databaseName);

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
    public async Task ExecuteDifferentialBackupAsync_ShouldMarkJobAsFailed_WhenBackupFails()
    {
        // Arrange
        var databaseName = "MyHospitalDB";
        var errorMessage = "Disk full";

        _mockBackupJobRepository
            .Setup(x => x.HasSuccessfulFullBackupAsync(databaseName))
            .ReturnsAsync(true);

        _mockBackupExecutor
            .Setup(x => x.ExecuteDifferentialBackupAsync(databaseName, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        _mockBackupJobRepository
            .Setup(x => x.CreateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);

        _mockBackupJobRepository
            .Setup(x => x.UpdateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);

        // Act
        var act = async () => await _backupService.ExecuteDifferentialBackupAsync(databaseName);

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
    public async Task ExecuteDifferentialBackupAsync_ShouldThrowArgumentException_WhenDatabaseNameEmpty(string databaseName)
    {
        // Act
        var act = async () => await _backupService.ExecuteDifferentialBackupAsync(databaseName);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Database name cannot be empty.*");
    }

    [Fact]
    public void DifferentialBackupFilePath_ShouldContainDIFFInName()
    {
        // Arrange
        var service = new BackupFilePathService(@"C:\Backups");
        var databaseName = "MyHospitalDB";

        // Act
        var filePath = service.GenerateBackupFilePath(databaseName, BackupType.Differential);

        // Assert
        filePath.Should().Contain("_DIFF_");
        filePath.Should().EndWith(".bak");
        Path.GetFileName(filePath).Should().MatchRegex(@"^MyHospitalDB_DIFF_\d{8}_\d{4}\.bak$");
    }
}
