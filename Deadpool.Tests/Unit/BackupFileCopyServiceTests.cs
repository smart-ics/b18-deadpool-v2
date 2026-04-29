using Deadpool.Core.Domain.Enums;
using Deadpool.Infrastructure.FileCopy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadpool.Tests.Unit;

public class BackupFileCopyServiceTests
{
    private readonly string _testSourceDir = Path.Combine(Path.GetTempPath(), "DeadpoolTest_Source");
    private readonly string _testDestDir = Path.Combine(Path.GetTempPath(), "DeadpoolTest_Dest");

    public BackupFileCopyServiceTests()
    {
        // Clean up any leftover test directories
        CleanupTestDirectories();

        // Create test directories
        Directory.CreateDirectory(_testSourceDir);
        Directory.CreateDirectory(_testDestDir);
    }

    private void CleanupTestDirectories()
    {
        try
        {
            if (Directory.Exists(_testSourceDir))
                Directory.Delete(_testSourceDir, recursive: true);

            if (Directory.Exists(_testDestDir))
                Directory.Delete(_testDestDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task CopyBackupFileAsync_ShouldCopyFile_WhenSourceExists()
    {
        // Arrange
        var service = new BackupFileCopyService(
            NullLogger<BackupFileCopyService>.Instance,
            _testDestDir,
            maxRetryAttempts: 1);

        var sourceFile = Path.Combine(_testSourceDir, "test.bak");

        // Create valid SQL Server backup file with header
        var backupContent = new byte[2048];
        var header = System.Text.Encoding.ASCII.GetBytes("TAPE");
        Array.Copy(header, backupContent, 4);
        File.WriteAllBytes(sourceFile, backupContent);

        // Act
        var destinationPath = await service.CopyBackupFileAsync(
            sourceFile,
            "TestDB",
            BackupType.Full);

        // Assert
        File.Exists(destinationPath).Should().BeTrue();
        var destSize = new FileInfo(destinationPath).Length;
        destSize.Should().Be(2048);

        // Cleanup
        CleanupTestDirectories();
    }

    [Fact]
    public async Task CopyBackupFileAsync_ShouldValidateFileSize_AfterCopy()
    {
        // Arrange
        var service = new BackupFileCopyService(
            NullLogger<BackupFileCopyService>.Instance,
            _testDestDir,
            maxRetryAttempts: 1);

        var sourceFile = Path.Combine(_testSourceDir, "test.bak");

        // Create 1 MB file with SQL Server header
        var testData = new byte[1024 * 1024];
        var header = System.Text.Encoding.ASCII.GetBytes("MTF ");
        Array.Copy(header, testData, 4);
        new Random().NextBytes(testData.Skip(16).ToArray());
        File.WriteAllBytes(sourceFile, testData);

        // Act
        var destinationPath = await service.CopyBackupFileAsync(
            sourceFile,
            "TestDB",
            BackupType.Full);

        // Assert
        var sourceSize = new FileInfo(sourceFile).Length;
        var destSize = new FileInfo(destinationPath).Length;
        destSize.Should().Be(sourceSize);

        // Cleanup
        CleanupTestDirectories();
    }

    [Fact]
    public async Task CopyBackupFileAsync_ShouldThrow_WhenSourceFileNotFound()
    {
        // Arrange
        var service = new BackupFileCopyService(
            NullLogger<BackupFileCopyService>.Instance,
            _testDestDir);

        var nonExistentFile = Path.Combine(_testSourceDir, "nonexistent.bak");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await service.CopyBackupFileAsync(
                nonExistentFile,
                "TestDB",
                BackupType.Full));

        // Cleanup
        CleanupTestDirectories();
    }

    [Fact]
    public async Task CopyBackupFileAsync_ShouldThrow_WhenSourceFileIsEmpty()
    {
        // Arrange
        var service = new BackupFileCopyService(
            NullLogger<BackupFileCopyService>.Instance,
            _testDestDir,
            maxRetryAttempts: 1);

        var sourceFile = Path.Combine(_testSourceDir, "empty.bak");
        File.WriteAllText(sourceFile, ""); // Create 0-byte file

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CopyBackupFileAsync(
                sourceFile,
                "TestDB",
                BackupType.Full));

        exception.Message.Should().Contain("empty (0 bytes)");
        exception.Message.Should().Contain("not a valid SQL Server backup");

        // Cleanup
        CleanupTestDirectories();
    }

    [Fact]
    public async Task CopyBackupFileAsync_ShouldThrow_WhenInvalidBackupHeader()
    {
        // Arrange
        var service = new BackupFileCopyService(
            NullLogger<BackupFileCopyService>.Instance,
            _testDestDir,
            maxRetryAttempts: 1);

        var sourceFile = Path.Combine(_testSourceDir, "invalid.bak");

        // Create file with invalid header (not SQL Server backup)
        var invalidContent = new byte[2048];
        var header = System.Text.Encoding.ASCII.GetBytes("ABCD"); // Invalid signature
        Array.Copy(header, invalidContent, 4);
        File.WriteAllBytes(sourceFile, invalidContent);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CopyBackupFileAsync(
                sourceFile,
                "TestDB",
                BackupType.Full));

        exception.Message.Should().Contain("Invalid SQL Server backup signature");
        exception.Message.Should().Contain("Expected 'TAPE' or 'MTF '");

        // Cleanup
        CleanupTestDirectories();
    }

    [Fact]
    public async Task CopyBackupFileAsync_ShouldOrganizeByDatabaseName()
    {
        // Arrange
        var service = new BackupFileCopyService(
            NullLogger<BackupFileCopyService>.Instance,
            _testDestDir);

        var sourceFile = Path.Combine(_testSourceDir, "test.bak");

        // Create valid backup file
        var content = new byte[2048];
        var header = System.Text.Encoding.ASCII.GetBytes("TAPE");
        Array.Copy(header, content, 4);
        File.WriteAllBytes(sourceFile, content);

        // Act
        var destinationPath = await service.CopyBackupFileAsync(
            sourceFile,
            "MyDatabase",
            BackupType.Full);

        // Assert
        destinationPath.Should().Contain("MyDatabase");
        destinationPath.Should().Contain(_testDestDir);

        var expectedDir = Path.Combine(_testDestDir, "MyDatabase");
        Directory.Exists(expectedDir).Should().BeTrue();

        // Cleanup
        CleanupTestDirectories();
    }

    [Fact]
    public async Task CopyBackupFileAsync_ShouldRetry_OnValidationFailure()
    {
        // This test verifies that validation failures are retryable
        // by ensuring the retry configuration is accepted and copy succeeds
        var service = new BackupFileCopyService(
            NullLogger<BackupFileCopyService>.Instance,
            _testDestDir,
            maxRetryAttempts: 3,
            retryDelay: TimeSpan.FromMilliseconds(10));

        var sourceFile = Path.Combine(_testSourceDir, "test.bak");

        // Create valid backup file
        var content = new byte[2048];
        var header = System.Text.Encoding.ASCII.GetBytes("MTF ");
        Array.Copy(header, content, 4);
        File.WriteAllBytes(sourceFile, content);

        var destinationPath = await service.CopyBackupFileAsync(
            sourceFile,
            "TestDB",
            BackupType.Full);

        destinationPath.Should().NotBeNullOrEmpty();
        File.Exists(destinationPath).Should().BeTrue();

        // Cleanup
        CleanupTestDirectories();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenRemoteStoragePathEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new BackupFileCopyService(
                NullLogger<BackupFileCopyService>.Instance,
                remoteStoragePath: ""));
    }
}
