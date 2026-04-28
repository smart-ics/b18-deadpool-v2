using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class BackupJobTests
{
    [Fact]
    public void Constructor_ShouldCreateBackupJob_WhenValidParameters()
    {
        var databaseName = "MyHospitalDB";
        var backupType = BackupType.Full;
        var backupFilePath = @"C:\Backups\MyHospitalDB_FULL_20260428_0200.bak";

        var job = new BackupJob(databaseName, backupType, backupFilePath);

        job.DatabaseName.Should().Be(databaseName);
        job.BackupType.Should().Be(backupType);
        job.BackupFilePath.Should().Be(backupFilePath);
        job.Status.Should().Be(BackupStatus.Pending);
        job.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        job.EndTime.Should().BeNull();
        job.FileSizeBytes.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenDatabaseNameEmpty(string databaseName)
    {
        var act = () => new BackupJob(databaseName, BackupType.Full, @"C:\Backups\test.bak");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Database name cannot be empty.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenBackupFilePathEmpty(string backupFilePath)
    {
        var act = () => new BackupJob("MyDB", BackupType.Full, backupFilePath);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Backup file path cannot be empty.*");
    }

    [Fact]
    public void MarkAsRunning_ShouldChangeStatusToRunning_WhenStatusIsPending()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");

        job.MarkAsRunning();

        job.Status.Should().Be(BackupStatus.Running);
    }

    [Fact]
    public void MarkAsRunning_ShouldThrowInvalidOperationException_WhenStatusIsNotPending()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");
        job.MarkAsRunning();

        var act = () => job.MarkAsRunning();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark job as running. Current status: Running");
    }

    [Fact]
    public void MarkAsCompleted_ShouldChangeStatusToCompleted_WhenStatusIsRunning()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");
        job.MarkAsRunning();

        job.MarkAsCompleted(1024000);

        job.Status.Should().Be(BackupStatus.Completed);
        job.EndTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        job.FileSizeBytes.Should().Be(1024000);
    }

    [Fact]
    public void MarkAsCompleted_ShouldThrowInvalidOperationException_WhenStatusIsNotRunning()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");

        var act = () => job.MarkAsCompleted(1024000);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark job as completed. Current status: Pending");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MarkAsCompleted_ShouldThrowArgumentException_WhenFileSizeInvalid(long fileSize)
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");
        job.MarkAsRunning();

        var act = () => job.MarkAsCompleted(fileSize);

        act.Should().Throw<ArgumentException>()
            .WithMessage("File size must be greater than zero.*");
    }

    [Fact]
    public void MarkAsFailed_ShouldChangeStatusToFailed_WhenStatusIsPending()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");

        job.MarkAsFailed("Disk full");

        job.Status.Should().Be(BackupStatus.Failed);
        job.EndTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        job.ErrorMessage.Should().Be("Disk full");
    }

    [Fact]
    public void MarkAsFailed_ShouldChangeStatusToFailed_WhenStatusIsRunning()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");
        job.MarkAsRunning();

        job.MarkAsFailed("Connection lost");

        job.Status.Should().Be(BackupStatus.Failed);
        job.ErrorMessage.Should().Be("Connection lost");
    }

    [Fact]
    public void MarkAsFailed_ShouldThrowInvalidOperationException_WhenStatusIsCompleted()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");
        job.MarkAsRunning();
        job.MarkAsCompleted(1024000);

        var act = () => job.MarkAsFailed("Error");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark completed job as failed.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkAsFailed_ShouldThrowArgumentException_WhenErrorMessageEmpty(string errorMessage)
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");

        var act = () => job.MarkAsFailed(errorMessage);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Error message cannot be empty.*");
    }

    [Fact]
    public void GetDuration_ShouldReturnNull_WhenJobNotFinished()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");

        var duration = job.GetDuration();

        duration.Should().BeNull();
    }

    [Fact]
    public void GetDuration_ShouldReturnTimeSpan_WhenJobCompleted()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");
        job.MarkAsRunning();
        Thread.Sleep(100);
        job.MarkAsCompleted(1024000);

        var duration = job.GetDuration();

        duration.Should().NotBeNull();
        duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetDuration_ShouldReturnTimeSpan_WhenJobFailed()
    {
        var job = new BackupJob("MyDB", BackupType.Full, @"C:\Backups\test.bak");
        job.MarkAsRunning();
        Thread.Sleep(100);
        job.MarkAsFailed("Error");

        var duration = job.GetDuration();

        duration.Should().NotBeNull();
        duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
