using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;
using Deadpool.Core.Services;
using FluentAssertions;
using Moq;

namespace Deadpool.Tests.Services;

public class RestoreChainResolverTests
{
    private readonly Mock<IBackupJobRepository> _mockRepository;
    private readonly RestoreChainResolver _resolver;

    public RestoreChainResolverTests()
    {
        _mockRepository = new Mock<IBackupJobRepository>();
        _resolver = new RestoreChainResolver(_mockRepository.Object);
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_NoBackups_ReturnsInvalidPlan()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob>());

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", DateTime.UtcNow);

        // Assert
        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("No completed backups found");
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_NoFullBackup_ReturnsInvalidPlan()
    {
        // Arrange
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var logs = new List<BackupJob>
        {
            CreateCompletedLogBackup("Log1.trn", baseTime, TimeSpan.FromMinutes(10), 1000m)
        };

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(logs);

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", DateTime.UtcNow);

        // Assert
        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("No valid Full backup found");
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_FullOnlyRestore_ReturnsValidPlan()
    {
        // Arrange - Full backup taken yesterday, completed after 30 minutes
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup });

        // Restore to the full backup completion time
        var restorePoint = fullBackup.EndTime!.Value;

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeTrue();
        plan.FullBackup.Should().Be(fullBackup);
        plan.DifferentialBackup.Should().BeNull();
        plan.LogBackups.Should().BeEmpty();
        plan.RestoreSequence.Should().HaveCount(1);
        plan.RestoreSequence[0].Should().Be(fullBackup);
        plan.ActualRestorePoint.Should().Be(fullBackup.EndTime!.Value);
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_FullPlusDifferential_ReturnsValidPlan()
    {
        // Arrange - Full backup yesterday at 2am, Diff backup at 8am (6 hours later)
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));
        var diffBackup = CreateCompletedDifferentialBackup(
            "Diff.bak",
            baseTime.AddHours(6),
            TimeSpan.FromMinutes(15),
            baseFullLSN: fullBackup.CheckpointLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, diffBackup });

        // Restore to the differential backup completion time
        var restorePoint = diffBackup.EndTime!.Value;

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeTrue();
        plan.FullBackup.Should().Be(fullBackup);
        plan.DifferentialBackup.Should().Be(diffBackup);
        plan.LogBackups.Should().BeEmpty();
        plan.RestoreSequence.Should().HaveCount(2);
        plan.RestoreSequence[0].Should().Be(fullBackup);
        plan.RestoreSequence[1].Should().Be(diffBackup);
        plan.ActualRestorePoint.Should().Be(diffBackup.EndTime!.Value);
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_FullPlusLogs_ReturnsValidPlan()
    {
        // Arrange - Full backup at 2am, log backups every hour
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        var log1Start = fullBackup.EndTime!.Value.AddMinutes(5);
        var log1 = CreateCompletedLogBackup("Log1.trn", log1Start, TimeSpan.FromMinutes(10), fullBackup.LastLSN!.Value);

        var log2Start = log1.EndTime!.Value.AddMinutes(50);
        var log2 = CreateCompletedLogBackup("Log2.trn", log2Start, TimeSpan.FromMinutes(10), log1.LastLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1, log2 });

        // Restore to the last log backup completion time
        var restorePoint = log2.EndTime!.Value;

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeTrue();
        plan.FullBackup.Should().Be(fullBackup);
        plan.DifferentialBackup.Should().BeNull();
        plan.LogBackups.Should().HaveCount(2);
        plan.LogBackups[0].Should().Be(log1);
        plan.LogBackups[1].Should().Be(log2);
        plan.RestoreSequence.Should().HaveCount(3);
        plan.ActualRestorePoint.Should().Be(restorePoint);
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_FullPlusDiffPlusLogs_ReturnsValidPlan()
    {
        // Arrange - Full at 2am, Diff at 8am, Logs at 9am and 10am
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        var diffBackup = CreateCompletedDifferentialBackup(
            "Diff.bak",
            baseTime.AddHours(6),
            TimeSpan.FromMinutes(15),
            baseFullLSN: fullBackup.CheckpointLSN!.Value);

        var log1Start = diffBackup.EndTime!.Value.AddMinutes(45);
        var log1 = CreateCompletedLogBackup("Log1.trn", log1Start, TimeSpan.FromMinutes(10), diffBackup.LastLSN!.Value);

        var log2Start = log1.EndTime!.Value.AddMinutes(50);
        var log2 = CreateCompletedLogBackup("Log2.trn", log2Start, TimeSpan.FromMinutes(10), log1.LastLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, diffBackup, log1, log2 });

        // Restore to the last log backup completion time
        var restorePoint = log2.EndTime!.Value;

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeTrue();
        plan.FullBackup.Should().Be(fullBackup);
        plan.DifferentialBackup.Should().Be(diffBackup);
        plan.LogBackups.Should().HaveCount(2);
        plan.RestoreSequence.Should().HaveCount(4);
        plan.RestoreSequence[0].Should().Be(fullBackup);
        plan.RestoreSequence[1].Should().Be(diffBackup);
        plan.RestoreSequence[2].Should().Be(log1);
        plan.RestoreSequence[3].Should().Be(log2);
        plan.ActualRestorePoint.Should().Be(restorePoint);
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_PointInTimeRestore_SelectsMinimalLogChain()
    {
        // Arrange - Full at 2am, 4 hourly log backups
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        var log1Start = fullBackup.EndTime!.Value.AddMinutes(5);
        var log1 = CreateCompletedLogBackup("Log1.trn", log1Start, TimeSpan.FromMinutes(10), fullBackup.LastLSN!.Value);

        var log2Start = log1.EndTime!.Value.AddHours(1);
        var log2 = CreateCompletedLogBackup("Log2.trn", log2Start, TimeSpan.FromMinutes(10), log1.LastLSN!.Value);

        var log3Start = log2.EndTime!.Value.AddHours(1);
        var log3 = CreateCompletedLogBackup("Log3.trn", log3Start, TimeSpan.FromMinutes(10), log2.LastLSN!.Value);

        var log4Start = log3.EndTime!.Value.AddHours(1);
        var log4 = CreateCompletedLogBackup("Log4.trn", log4Start, TimeSpan.FromMinutes(10), log3.LastLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1, log2, log3, log4 });

        // Point-in-time restore: midway through log2's time window
        var restorePoint = log2.StartTime.AddMinutes(5);

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeTrue();
        plan.LogBackups.Should().HaveCount(2); // Only log1 and log2 needed
        plan.LogBackups[0].Should().Be(log1);
        plan.LogBackups[1].Should().Be(log2);
        plan.RestoreSequence.Should().NotContain(log3);
        plan.RestoreSequence.Should().NotContain(log4);
        plan.ActualRestorePoint.Should().Be(restorePoint); // Point-in-time with STOPAT
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_BrokenLSNChain_ReturnsInvalidPlan()
    {
        // Arrange - LSN gap between log1 and log2
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        var log1Start = fullBackup.EndTime!.Value.AddMinutes(5);
        var log1 = CreateCompletedLogBackup("Log1.trn", log1Start, TimeSpan.FromMinutes(10), fullBackup.LastLSN!.Value);

        // log2 has LSN gap - does not continue from log1
        var log2Start = log1.EndTime!.Value.AddHours(1);
        var log2 = CreateCompletedLogBackup("Log2.trn", log2Start, TimeSpan.FromMinutes(10), log1.LastLSN!.Value + 1000);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1, log2 });

        // Request restore to a point that would require log2 (after log1 ends)
        var restorePoint = log2.StartTime.AddMinutes(5);

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("beyond available log backup coverage");
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_RestorePointBeyondCoverage_ReturnsInvalidPlan()
    {
        // Arrange
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        var log1Start = fullBackup.EndTime!.Value.AddMinutes(5);
        var log1 = CreateCompletedLogBackup("Log1.trn", log1Start, TimeSpan.FromMinutes(10), fullBackup.LastLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1 });

        // Request restore to a point well beyond the last log
        var restorePoint = log1.EndTime!.Value.AddDays(1);

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("beyond available log backup coverage");
    }


    [Fact]
    public async Task ResolveRestoreChainAsync_OrphanedDifferential_IgnoresDifferential()
    {
        // Arrange
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        // Differential references a different Full backup (orphaned)
        var orphanedDiff = CreateCompletedDifferentialBackup(
            "Diff.bak",
            baseTime.AddHours(4),
            TimeSpan.FromMinutes(15),
            baseFullLSN: fullBackup.CheckpointLSN!.Value + 9999);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, orphanedDiff });

        // Restore to the full backup completion time
        var restorePoint = fullBackup.EndTime!.Value;

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeTrue();
        plan.FullBackup.Should().Be(fullBackup);
        plan.DifferentialBackup.Should().BeNull(); // Orphaned diff ignored
        plan.RestoreSequence.Should().HaveCount(1);
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_LatestValidDifferential_SelectedOverOlderDifferentials()
    {
        // Arrange
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        var diff1 = CreateCompletedDifferentialBackup(
            "Diff1.bak",
            baseTime.AddHours(3),
            TimeSpan.FromMinutes(15),
            baseFullLSN: fullBackup.CheckpointLSN!.Value);

        var diff2 = CreateCompletedDifferentialBackup(
            "Diff2.bak",
            baseTime.AddHours(6),
            TimeSpan.FromMinutes(15),
            baseFullLSN: fullBackup.CheckpointLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, diff1, diff2 });

        // Restore to the latest differential completion time
        var restorePoint = diff2.EndTime!.Value;

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeTrue();
        plan.DifferentialBackup.Should().Be(diff2); // Latest valid diff selected
        plan.RestoreSequence.Should().NotContain(diff1);
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_FullBackupMissingLSN_ReturnsInvalidPlan()
    {
        // Arrange
        var fullBackup = new BackupJob("TestDB", BackupType.Full, "Full.bak");
        fullBackup.MarkAsRunning();
        fullBackup.MarkAsCompleted(1024 * 1024 * 100);
        // No LSN metadata set

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup });

        var restorePoint = fullBackup.EndTime!.Value;

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("Full backup missing LSN metadata");
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_PointInTimeWithStopAt_ReturnsRequestedPoint()
    {
        // Arrange - Test STOPAT semantics
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        var log1Start = fullBackup.EndTime!.Value.AddMinutes(5);
        var log1 = CreateCompletedLogBackup("Log1.trn", log1Start, TimeSpan.FromHours(1), fullBackup.LastLSN!.Value);

        var log2Start = log1.EndTime!.Value.AddMinutes(5);
        var log2 = CreateCompletedLogBackup("Log2.trn", log2Start, TimeSpan.FromHours(1), log1.LastLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1, log2 });

        // Point-in-time restore: 30 minutes into log1's time window
        var restorePoint = log1.StartTime.AddMinutes(30);

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert - Validates STOPAT semantics
        plan.IsValid.Should().BeTrue();
        plan.LogBackups.Should().HaveCount(1); // Only log1 needed
        plan.LogBackups[0].Should().Be(log1);
        plan.ActualRestorePoint.Should().Be(restorePoint); // NOT log1.EndTime!
        plan.GetRestoreDescription().Should().Contain("Point-in-time");
    }

    [Fact]
    public async Task ResolveRestoreChainAsync_RestorePointBeforeFirstLog_ReturnsInvalidPlan()
    {
        // Arrange - Restore point is in the gap between full and first log
        var baseTime = DateTime.UtcNow.Date.AddDays(-1).AddHours(2);
        var fullBackup = CreateCompletedFullBackup("Full.bak", baseTime, TimeSpan.FromMinutes(30));

        // Log starts 2 hours after full completes (gap in coverage)
        var log1Start = fullBackup.EndTime!.Value.AddHours(2);
        var log1 = CreateCompletedLogBackup("Log1.trn", log1Start, TimeSpan.FromMinutes(10), fullBackup.LastLSN!.Value);

        _mockRepository.Setup(r => r.GetBackupsByDatabaseAsync("TestDB"))
            .ReturnsAsync(new List<BackupJob> { fullBackup, log1 });

        // Try to restore to a point in the gap (after full ends, before log1 starts)
        var restorePoint = fullBackup.EndTime!.Value.AddHours(1);

        // Act
        var plan = await _resolver.ResolveRestoreChainAsync("TestDB", restorePoint);

        // Assert
        plan.IsValid.Should().BeFalse();
        plan.FailureReason.Should().Contain("before the first log backup starts");
    }

    // Helper methods to create test data with deterministic timestamps

    private BackupJob CreateCompletedFullBackup(string fileName, DateTime startTime, TimeSpan duration)
    {
        var job = new BackupJob("TestDB", BackupType.Full, $@"C:\Backups\{fileName}");

        // Use backing field reflection to set StartTime
        var startTimeField = typeof(BackupJob).GetField("<StartTime>k__BackingField", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        startTimeField!.SetValue(job, startTime);

        // Use backing field reflection to set EndTime  
        var endTimeField = typeof(BackupJob).GetField("<EndTime>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        endTimeField!.SetValue(job, (DateTime?)startTime.Add(duration));

        // Use backing field reflection to set Status to Completed
        var statusField = typeof(BackupJob).GetField("<Status>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField!.SetValue(job, BackupStatus.Completed);

        // Use backing field reflection to set FileSizeBytes
        var fileSizeField = typeof(BackupJob).GetField("<FileSizeBytes>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fileSizeField!.SetValue(job, (long?)(1024 * 1024 * 100));

        // Set LSN metadata (requires Status == Completed)
        var baseTime = startTime.Ticks / TimeSpan.TicksPerHour;
        var checkpointLSN = 1000m + baseTime;
        job.SetLSNMetadata(
            firstLSN: checkpointLSN - 50,
            lastLSN: checkpointLSN + 50,
            databaseBackupLSN: null,
            checkpointLSN: checkpointLSN);

        return job;
    }

    private BackupJob CreateCompletedDifferentialBackup(
        string fileName, 
        DateTime startTime, 
        TimeSpan duration, 
        decimal baseFullLSN)
    {
        var job = new BackupJob("TestDB", BackupType.Differential, $@"C:\Backups\{fileName}");

        // Use backing field reflection to set StartTime
        var startTimeField = typeof(BackupJob).GetField("<StartTime>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        startTimeField!.SetValue(job, startTime);

        // Use backing field reflection to set EndTime
        var endTimeField = typeof(BackupJob).GetField("<EndTime>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        endTimeField!.SetValue(job, (DateTime?)startTime.Add(duration));

        // Use backing field reflection to set Status to Completed
        var statusField = typeof(BackupJob).GetField("<Status>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField!.SetValue(job, BackupStatus.Completed);

        // Use backing field reflection to set FileSizeBytes
        var fileSizeField = typeof(BackupJob).GetField("<FileSizeBytes>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fileSizeField!.SetValue(job, (long?)(1024 * 1024 * 50));

        // Set LSN metadata (requires Status == Completed)
        job.SetLSNMetadata(
            firstLSN: baseFullLSN + 100,
            lastLSN: baseFullLSN + 200,
            databaseBackupLSN: baseFullLSN,
            checkpointLSN: null);

        return job;
    }

    private BackupJob CreateCompletedLogBackup(
        string fileName, 
        DateTime startTime, 
        TimeSpan duration, 
        decimal firstLSN)
    {
        var job = new BackupJob("TestDB", BackupType.TransactionLog, $@"C:\Backups\{fileName}");

        // Use backing field reflection to set StartTime
        var startTimeField = typeof(BackupJob).GetField("<StartTime>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        startTimeField!.SetValue(job, startTime);

        // Use backing field reflection to set EndTime
        var endTimeField = typeof(BackupJob).GetField("<EndTime>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        endTimeField!.SetValue(job, (DateTime?)startTime.Add(duration));

        // Use backing field reflection to set Status to Completed
        var statusField = typeof(BackupJob).GetField("<Status>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField!.SetValue(job, BackupStatus.Completed);

        // Use backing field reflection to set FileSizeBytes
        var fileSizeField = typeof(BackupJob).GetField("<FileSizeBytes>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fileSizeField!.SetValue(job, (long?)(1024 * 1024 * 10));

        // Set LSN metadata (requires Status == Completed)
        job.SetLSNMetadata(
            firstLSN: firstLSN,
            lastLSN: firstLSN + 50,
            databaseBackupLSN: null,
            checkpointLSN: null);

        return job;
    }
}
