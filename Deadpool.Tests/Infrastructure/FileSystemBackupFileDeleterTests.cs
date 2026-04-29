using Deadpool.Infrastructure.Storage;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Infrastructure;

public class FileSystemBackupFileDeleterTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemBackupFileDeleter _deleter;

    public FileSystemBackupFileDeleterTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RetentionTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _deleter = new FileSystemBackupFileDeleter();
    }

    [Fact]
    public async Task DeleteBackupFileAsync_ShouldDeleteExistingFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test_backup.bak");
        await File.WriteAllTextAsync(testFile, "Test backup content");
        File.Exists(testFile).Should().BeTrue();

        // Act
        var result = await _deleter.DeleteBackupFileAsync(testFile);

        // Assert
        result.Should().BeTrue();
        File.Exists(testFile).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBackupFileAsync_ShouldReturnTrue_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.bak");

        // Act
        var result = await _deleter.DeleteBackupFileAsync(nonExistentFile);

        // Assert: Non-existent file is considered successfully deleted
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteBackupFileAsync_ShouldReturnFalse_WhenDeletionFails()
    {
        // Arrange: Create a file and make it read-only (simulate access denial)
        var testFile = Path.Combine(_testDirectory, "readonly.bak");
        await File.WriteAllTextAsync(testFile, "Test content");
        var fileInfo = new FileInfo(testFile);
        fileInfo.IsReadOnly = true;

        try
        {
            // Act
            var result = await _deleter.DeleteBackupFileAsync(testFile);

            // Assert: Should return false on failure
            result.Should().BeFalse();
            File.Exists(testFile).Should().BeTrue(); // File should still exist
        }
        finally
        {
            // Cleanup: Remove read-only attribute
            fileInfo.IsReadOnly = false;
            File.Delete(testFile);
        }
    }

    [Fact]
    public async Task DeleteBackupFileAsync_ShouldThrow_WhenFilePathEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _deleter.DeleteBackupFileAsync(""));
    }

    [Fact]
    public async Task FileExistsAsync_ShouldReturnTrue_WhenFileExists()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "exists.bak");
        await File.WriteAllTextAsync(testFile, "Test content");

        // Act
        var result = await _deleter.FileExistsAsync(testFile);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.bak");

        // Act
        var result = await _deleter.FileExistsAsync(nonExistentFile);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task FileExistsAsync_ShouldThrow_WhenFilePathEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _deleter.FileExistsAsync(""));
    }

    [Fact]
    public async Task FileExistsAsync_ShouldReturnFalse_WhenPathIsDirectory()
    {
        // Arrange: Use the test directory itself
        // Act
        var result = await _deleter.FileExistsAsync(_testDirectory);

        // Assert: Directory should not be considered a file
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBackupFileAsync_ShouldDeleteMultipleFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "backup1.bak");
        var file2 = Path.Combine(_testDirectory, "backup2.bak");
        var file3 = Path.Combine(_testDirectory, "backup3.bak");

        await File.WriteAllTextAsync(file1, "Content 1");
        await File.WriteAllTextAsync(file2, "Content 2");
        await File.WriteAllTextAsync(file3, "Content 3");

        // Act
        var result1 = await _deleter.DeleteBackupFileAsync(file1);
        var result2 = await _deleter.DeleteBackupFileAsync(file2);
        var result3 = await _deleter.DeleteBackupFileAsync(file3);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();

        File.Exists(file1).Should().BeFalse();
        File.Exists(file2).Should().BeFalse();
        File.Exists(file3).Should().BeFalse();
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }
}
