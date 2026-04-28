using Deadpool.Core.Services;
using Deadpool.Core.Domain.Enums;
using FluentAssertions;

namespace Deadpool.Tests.Unit;

public class BackupFilePathServiceTests
{
    [Fact]
    public void Constructor_ShouldCreateService_WhenValidDirectory()
    {
        var directory = @"C:\Backups";

        var service = new BackupFilePathService(directory);

        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenDirectoryEmpty(string directory)
    {
        var act = () => new BackupFilePathService(directory);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Backup directory cannot be empty.*");
    }

    [Fact]
    public void GenerateBackupFilePath_ShouldGenerateCorrectPath_ForFullBackup()
    {
        var service = new BackupFilePathService(@"C:\Backups");
        var databaseName = "MyHospitalDB";

        var filePath = service.GenerateBackupFilePath(databaseName, BackupType.Full);

        filePath.Should().StartWith(@"C:\Backups\MyHospitalDB_FULL_");
        filePath.Should().EndWith(".bak");
    }

    [Fact]
    public void GenerateBackupFilePath_ShouldGenerateCorrectPath_ForDifferentialBackup()
    {
        var service = new BackupFilePathService(@"C:\Backups");
        var databaseName = "MyHospitalDB";

        var filePath = service.GenerateBackupFilePath(databaseName, BackupType.Differential);

        filePath.Should().StartWith(@"C:\Backups\MyHospitalDB_DIFF_");
        filePath.Should().EndWith(".bak");
    }

    [Fact]
    public void GenerateBackupFilePath_ShouldGenerateCorrectPath_ForTransactionLogBackup()
    {
        var service = new BackupFilePathService(@"C:\Backups");
        var databaseName = "MyHospitalDB";

        var filePath = service.GenerateBackupFilePath(databaseName, BackupType.TransactionLog);

        filePath.Should().StartWith(@"C:\Backups\MyHospitalDB_LOG_");
        filePath.Should().EndWith(".trn");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateBackupFilePath_ShouldThrowArgumentException_WhenDatabaseNameEmpty(string databaseName)
    {
        var service = new BackupFilePathService(@"C:\Backups");

        var act = () => service.GenerateBackupFilePath(databaseName, BackupType.Full);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Database name cannot be empty.*");
    }

    [Fact]
    public void GenerateBackupFilePath_ShouldIncludeDateAndTime_InFileName()
    {
        var service = new BackupFilePathService(@"C:\Backups");
        var databaseName = "MyDB";
        var now = DateTime.UtcNow;

        var filePath = service.GenerateBackupFilePath(databaseName, BackupType.Full);

        var fileName = Path.GetFileName(filePath);
        fileName.Should().Contain(now.ToString("yyyyMMdd"));
    }
}
