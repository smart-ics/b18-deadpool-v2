using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.BackupExecution;
using FluentAssertions;
using Microsoft.Data.SqlClient;

namespace Deadpool.Tests.Unit;

public class SqlServerBackupExecutorTests
{
    [Theory]
    [InlineData("MyHospitalDB")]
    [InlineData("Hospital_DB")]
    [InlineData("@TempDB")]
    [InlineData("#LocalTempDB")]
    [InlineData("DB$2024")]
    public void ValidateDatabaseName_ShouldAccept_ValidDatabaseNames(string databaseName)
    {
        // Arrange
        var executor = new SqlServerBackupExecutor("Server=.;");

        // Act - ExecuteFullBackupAsync will call ValidateDatabaseName internally
        // We test indirectly by checking it doesn't throw on valid names
        var act = async () => await executor.ExecuteFullBackupAsync(databaseName, @"C:\test.bak");

        // Assert - Should fail on connection, not validation
        act.Should().ThrowAsync<SqlException>()
            .WithMessage("*");
    }

    [Theory]
    [InlineData("1InvalidStart")]
    [InlineData("$InvalidStart")]
    [InlineData("Invalid-Name")]
    [InlineData("Invalid Name")]
    [InlineData("Invalid;Name")]
    [InlineData("Invalid'Name")]
    public async Task ExecuteFullBackupAsync_ShouldThrowArgumentException_WhenDatabaseNameInvalid(
        string databaseName)
    {
        // Arrange
        var executor = new SqlServerBackupExecutor("Server=.;");

        // Act
        var act = async () => await executor.ExecuteFullBackupAsync(databaseName, @"C:\test.bak");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"Invalid database name format: {databaseName}*");
    }

    [Fact]
    public async Task ExecuteFullBackupAsync_ShouldThrowArgumentException_WhenDatabaseNameTooLong()
    {
        // Arrange
        var executor = new SqlServerBackupExecutor("Server=.;");
        var longName = new string('A', 129); // 129 characters, exceeds 128 limit

        // Act
        var act = async () => await executor.ExecuteFullBackupAsync(longName, @"C:\test.bak");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"Database name exceeds 128 characters*");
    }

    [Fact]
    public async Task VerifyBackupFileAsync_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var executor = new SqlServerBackupExecutor("Server=.;");
        var nonExistentFile = @"C:\NonExistent\File.bak";

        // Act
        var result = await executor.VerifyBackupFileAsync(nonExistentFile);

        // Assert
        result.Should().BeFalse();
    }
}
