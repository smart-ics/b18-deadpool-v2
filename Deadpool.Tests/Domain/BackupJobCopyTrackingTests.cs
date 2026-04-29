using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class BackupJobCopyTrackingTests
{
    [Fact]
    public void MarkCopyStarted_ShouldRecordCopyMetadata()
    {
        // Arrange
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\backup.bak");
        job.MarkAsRunning();
        job.MarkAsCompleted(1024);

        // Act
        job.MarkCopyStarted("\\\\Server\\Backups\\TestDB\\backup.bak");

        // Assert
        job.CopyStarted.Should().BeTrue();
        job.CopyStartTime.Should().NotBeNull();
        job.CopyDestinationPath.Should().Be("\\\\Server\\Backups\\TestDB\\backup.bak");
    }

    [Fact]
    public void MarkCopyStarted_ShouldThrow_WhenJobNotCompleted()
    {
        // Arrange
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\backup.bak");

        // Act & Assert
        var act = () => job.MarkCopyStarted("\\\\Server\\backup.bak");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-completed*");
    }

    [Fact]
    public void MarkCopyCompleted_ShouldRecordCompletionTime()
    {
        // Arrange
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\backup.bak");
        job.MarkAsRunning();
        job.MarkAsCompleted(1024);
        job.MarkCopyStarted("\\\\Server\\backup.bak");

        // Act
        job.MarkCopyCompleted();

        // Assert
        job.CopyCompleted.Should().BeTrue();
        job.CopyEndTime.Should().NotBeNull();
        job.GetCopyDuration().Should().NotBeNull();
    }

    [Fact]
    public void MarkCopyCompleted_ShouldThrow_WhenCopyNotStarted()
    {
        // Arrange
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\backup.bak");
        job.MarkAsRunning();
        job.MarkAsCompleted(1024);

        // Act & Assert
        var act = () => job.MarkCopyCompleted();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*copy has not started*");
    }

    [Fact]
    public void MarkCopyFailed_ShouldRecordErrorMessage()
    {
        // Arrange
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\backup.bak");
        job.MarkAsRunning();
        job.MarkAsCompleted(1024);
        job.MarkCopyStarted("\\\\Server\\backup.bak");

        // Act
        job.MarkCopyFailed("Network share unavailable");

        // Assert
        job.CopyCompleted.Should().BeFalse();
        job.CopyEndTime.Should().NotBeNull();
        job.CopyErrorMessage.Should().Be("Network share unavailable");
    }

    [Fact]
    public void GetCopyDuration_ShouldReturnNull_WhenCopyNotStarted()
    {
        // Arrange
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\backup.bak");

        // Act
        var duration = job.GetCopyDuration();

        // Assert
        duration.Should().BeNull();
    }

    [Fact]
    public void GetCopyDuration_ShouldCalculateDuration_WhenCopyCompleted()
    {
        // Arrange
        var job = new BackupJob("TestDB", BackupType.Full, "C:\\backup.bak");
        job.MarkAsRunning();
        job.MarkAsCompleted(1024);
        job.MarkCopyStarted("\\\\Server\\backup.bak");

        Task.Delay(50).Wait(); // Small delay

        job.MarkCopyCompleted();

        // Act
        var duration = job.GetCopyDuration();

        // Assert
        duration.Should().NotBeNull();
        duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
