using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;

namespace Deadpool.Tests.Unit;

public class BackupServiceTests
{
    private readonly Mock<IBackupExecutor> _mockBackupExecutor;
    private readonly Mock<IBackupJobRepository> _mockBackupJobRepository;
    private readonly Mock<IDatabaseMetadataService> _mockMetadataService;
    private readonly BackupFilePathService _filePathService;
    private readonly BackupService _backupService;

    public BackupServiceTests()
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
    public void Constructor_ShouldThrowArgumentNullException_WhenBackupExecutorIsNull()
    {
        var act = () => new BackupService(
            null!,
            _mockBackupJobRepository.Object,
            _filePathService,
            _mockMetadataService.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("backupExecutor");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenBackupJobRepositoryIsNull()
    {
        var act = () => new BackupService(
            _mockBackupExecutor.Object,
            null!,
            _filePathService,
            _mockMetadataService.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("backupJobRepository");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenFilePathServiceIsNull()
    {
        var act = () => new BackupService(
            _mockBackupExecutor.Object,
            _mockBackupJobRepository.Object,
            null!,
            _mockMetadataService.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("filePathService");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenMetadataServiceIsNull()
    {
        var act = () => new BackupService(
            _mockBackupExecutor.Object,
            _mockBackupJobRepository.Object,
            _filePathService,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("databaseMetadataService");
    }

    [Fact]
    public async Task ExecuteFullBackupAsync_ShouldPersistJobBeforeExecution()
    {
        var databaseName = "MyHospitalDB";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.bak");
        File.WriteAllText(tempFile, "test backup");

        try
        {
            var callSequence = new List<string>();

            _mockBackupJobRepository
                .Setup(x => x.CreateAsync(It.IsAny<BackupJob>()))
                .Callback(() => callSequence.Add("CreateAsync"))
                .Returns(Task.CompletedTask);

            _mockBackupJobRepository
                .Setup(x => x.UpdateAsync(It.IsAny<BackupJob>()))
                .Callback(() => callSequence.Add("UpdateAsync"))
                .Returns(Task.CompletedTask);

            _mockBackupExecutor
                .Setup(x => x.ExecuteFullBackupAsync(databaseName, It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    callSequence.Add("ExecuteBackup");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(tempFile, path, true);
                })
                .Returns(Task.CompletedTask);

            await _backupService.ExecuteFullBackupAsync(databaseName);

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
    public async Task ExecuteFullBackupAsync_ShouldExecuteBackupAndUpdateStatus_WhenSuccessful()
    {
        var databaseName = "MyHospitalDB";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.bak");
        File.WriteAllText(tempFile, "test backup");

        try
        {
            _mockBackupExecutor
                .Setup(x => x.ExecuteFullBackupAsync(databaseName, It.IsAny<string>()))
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

            var result = await _backupService.ExecuteFullBackupAsync(databaseName);

            result.Should().NotBeNull();
            result.Status.Should().Be(BackupStatus.Completed);
            _mockBackupExecutor.Verify(x => x.ExecuteFullBackupAsync(databaseName, It.IsAny<string>()), Times.Once);
            _mockBackupJobRepository.Verify(x => x.CreateAsync(It.IsAny<BackupJob>()), Times.Once);
            _mockBackupJobRepository.Verify(x => x.UpdateAsync(It.IsAny<BackupJob>()), Times.Exactly(2));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteFullBackupAsync_ShouldMarkJobAsFailed_WhenBackupFails()
    {
        var databaseName = "MyHospitalDB";
        var errorMessage = "Disk full";

        _mockBackupExecutor
            .Setup(x => x.ExecuteFullBackupAsync(databaseName, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));

        _mockBackupJobRepository
            .Setup(x => x.CreateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);

        _mockBackupJobRepository
            .Setup(x => x.UpdateAsync(It.IsAny<BackupJob>()))
            .Returns(Task.CompletedTask);

        var act = async () => await _backupService.ExecuteFullBackupAsync(databaseName);

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
    public async Task ExecuteFullBackupAsync_ShouldThrowArgumentException_WhenDatabaseNameEmpty(string databaseName)
    {
        var act = async () => await _backupService.ExecuteFullBackupAsync(databaseName);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Database name cannot be empty.*");
    }

    [Fact]
    public async Task VerifyBackupAsync_ShouldReturnTrue_WhenBackupIsValid()
    {
        var backupFilePath = @"C:\Backups\test.bak";

        _mockBackupExecutor
            .Setup(x => x.VerifyBackupFileAsync(backupFilePath))
            .ReturnsAsync(true);

        var result = await _backupService.VerifyBackupAsync(backupFilePath);

        result.Should().BeTrue();
        _mockBackupExecutor.Verify(x => x.VerifyBackupFileAsync(backupFilePath), Times.Once);
    }

    [Fact]
    public async Task VerifyBackupAsync_ShouldReturnFalse_WhenBackupIsInvalid()
    {
        var backupFilePath = @"C:\Backups\test.bak";

        _mockBackupExecutor
            .Setup(x => x.VerifyBackupFileAsync(backupFilePath))
            .ReturnsAsync(false);

        var result = await _backupService.VerifyBackupAsync(backupFilePath);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyBackupAsync_ShouldThrowArgumentException_WhenBackupFilePathEmpty(string backupFilePath)
    {
        var act = async () => await _backupService.VerifyBackupAsync(backupFilePath);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Backup file path cannot be empty.*");
    }
}
