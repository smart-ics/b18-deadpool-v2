using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadpool.Tests.Services;

public class RestorePlanValidatorServiceTests
{
    private readonly RestorePlanValidatorService _service =
        new(NullLogger<RestorePlanValidatorService>.Instance);

    [Fact]
    public void Validate_ValidPlanWithAccessibleFiles_ReturnsValid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var diffPath = CreateTempFile(tempDir, "diff.bak");
            var log1Path = CreateTempFile(tempDir, "log1.trn");
            var log2Path = CreateTempFile(tempDir, "log2.trn");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-4));
            var diff = CreateCompletedDifferentialBackup(diffPath, DateTime.UtcNow.AddHours(-3), full.CheckpointLSN!.Value, full.EndTime!.Value.AddMinutes(5));
            var log1 = CreateCompletedLogBackup(log1Path, DateTime.UtcNow.AddHours(-2), diff.LastLSN!.Value);
            var log2 = CreateCompletedLogBackup(log2Path, DateTime.UtcNow.AddHours(-1), log1.LastLSN!.Value);

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                log2.StartTime.AddMinutes(5),
                full,
                diff,
                new List<BackupJob> { log1, log2 },
                log2.StartTime.AddMinutes(5));

            var result = _service.Validate(plan);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_PendingPath_ReturnsInvalid()
    {
        var full = CreateCompletedFullBackup("PENDING_full.bak", DateTime.UtcNow.AddHours(-2));
        var plan = RestorePlan.CreateValidPlan(
            "TestDB",
            full.EndTime!.Value,
            full,
            differentialBackup: null,
            logBackups: Array.Empty<BackupJob>(),
            actualRestorePoint: full.EndTime.Value);

        var result = _service.Validate(plan);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("missing or pending placeholder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_LockedFile_ReturnsInvalid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-2));

            using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                full.EndTime!.Value,
                full,
                differentialBackup: null,
                logBackups: Array.Empty<BackupJob>(),
                actualRestorePoint: full.EndTime.Value);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("inaccessible or locked", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_DifferentialLsnMismatch_ReturnsInvalid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var diffPath = CreateTempFile(tempDir, "diff.bak");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-4));
            var diff = CreateCompletedDifferentialBackup(diffPath, DateTime.UtcNow.AddHours(-3), full.CheckpointLSN!.Value + 999m, full.EndTime!.Value.AddMinutes(5));

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                diff.EndTime!.Value,
                full,
                diff,
                Array.Empty<BackupJob>(),
                diff.EndTime.Value);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("LSN mismatch", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_TargetBeyondLastLogCoverage_ReturnsInvalid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var logPath = CreateTempFile(tempDir, "log1.trn");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-4));
            var log = CreateCompletedLogBackup(logPath, DateTime.UtcNow.AddHours(-2), full.LastLSN!.Value);

            var target = log.EndTime!.Value.AddMinutes(30);
            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                target,
                full,
                differentialBackup: null,
                logBackups: new List<BackupJob> { log },
                actualRestorePoint: target);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("beyond available log backup coverage", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_MissingDifferentialWithValidLogChain_ReturnsValid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var log1Path = CreateTempFile(tempDir, "log1.trn");
            var log2Path = CreateTempFile(tempDir, "log2.trn");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-5));
            var log1 = CreateCompletedLogBackup(log1Path, full.EndTime!.Value.AddMinutes(5), full.LastLSN!.Value);
            var log2 = CreateCompletedLogBackup(log2Path, log1.EndTime!.Value.AddMinutes(5), log1.LastLSN!.Value);

            var target = log2.StartTime.AddMinutes(5);
            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                target,
                full,
                differentialBackup: null,
                logBackups: new List<BackupJob> { log1, log2 },
                actualRestorePoint: target);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_BaseWithoutLastLsn_WithConsecutiveLogs_ReturnsValid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var log1Path = CreateTempFile(tempDir, "log1.trn");
            var log2Path = CreateTempFile(tempDir, "log2.trn");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-5));
            full.SetLSNMetadata(full.FirstLSN, null, full.DatabaseBackupLSN, full.CheckpointLSN);

            var log1 = CreateCompletedLogBackup(log1Path, full.EndTime!.Value.AddMinutes(5), 5000m);
            var log2 = CreateCompletedLogBackup(log2Path, log1.EndTime!.Value.AddMinutes(5), log1.LastLSN!.Value);

            var target = log2.StartTime.AddMinutes(5);
            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                target,
                full,
                differentialBackup: null,
                logBackups: new List<BackupJob> { log1, log2 },
                actualRestorePoint: target);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_FullOnlyRestore_ReturnsValid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-3));

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                full.EndTime!.Value,
                full,
                differentialBackup: null,
                logBackups: Array.Empty<BackupJob>(),
                actualRestorePoint: full.EndTime.Value);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_StopAtExactlyAtFullBoundary_ReturnsValid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-3));

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                full.EndTime!.Value,
                full,
                differentialBackup: null,
                logBackups: Array.Empty<BackupJob>(),
                actualRestorePoint: full.EndTime.Value);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_StopAtExactlyAtDifferentialBoundary_ReturnsValid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var diffPath = CreateTempFile(tempDir, "diff.bak");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-4));
            var diff = CreateCompletedDifferentialBackup(diffPath, full.EndTime!.Value.AddMinutes(5), full.CheckpointLSN!.Value, full.EndTime.Value.AddMinutes(5));

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                diff.EndTime!.Value,
                full,
                diff,
                Array.Empty<BackupJob>(),
                diff.EndTime.Value);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_StopAtExactlyAtLogBoundary_ReturnsValid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var logPath = CreateTempFile(tempDir, "log1.trn");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-4));
            var log = CreateCompletedLogBackup(logPath, full.EndTime!.Value.AddMinutes(5), full.LastLSN!.Value);

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                log.EndTime!.Value,
                full,
                differentialBackup: null,
                logBackups: new List<BackupJob> { log },
                actualRestorePoint: log.EndTime.Value);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_NullFilePath_ReturnsInvalid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-2));
            SetBackupPath(full, null);

            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                full.EndTime!.Value,
                full,
                differentialBackup: null,
                logBackups: Array.Empty<BackupJob>(),
                actualRestorePoint: full.EndTime.Value);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("missing or pending placeholder", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_MalformedFilePath_ReturnsInvalid()
    {
        var full = CreateCompletedFullBackup("bad\0path.bak", DateTime.UtcNow.AddHours(-2));

        var plan = RestorePlan.CreateValidPlan(
            "TestDB",
            full.EndTime!.Value,
            full,
            differentialBackup: null,
            logBackups: Array.Empty<BackupJob>(),
            actualRestorePoint: full.EndTime.Value);

        var result = _service.Validate(plan);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("missing or pending placeholder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PartialLogChainGap_ReturnsInvalid()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var log1Path = CreateTempFile(tempDir, "log1.trn");
            var log2Path = CreateTempFile(tempDir, "log2.trn");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-5));
            var log1 = CreateCompletedLogBackup(log1Path, full.EndTime!.Value.AddMinutes(5), full.LastLSN!.Value);
            var log2 = CreateCompletedLogBackup(log2Path, log1.EndTime!.Value.AddMinutes(5), log1.LastLSN!.Value + 999m);

            var target = log2.EndTime!.Value;
            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                target,
                full,
                differentialBackup: null,
                logBackups: new List<BackupJob> { log1, log2 },
                actualRestorePoint: target);

            var result = _service.Validate(plan);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("Broken transaction log chain", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnorderedLogInput_ReturnsInvalidDeterministically()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var fullPath = CreateTempFile(tempDir, "full.bak");
            var log1Path = CreateTempFile(tempDir, "log1.trn");
            var log2Path = CreateTempFile(tempDir, "log2.trn");

            var full = CreateCompletedFullBackup(fullPath, DateTime.UtcNow.AddHours(-5));
            var log1 = CreateCompletedLogBackup(log1Path, full.EndTime!.Value.AddMinutes(5), full.LastLSN!.Value);
            var log2 = CreateCompletedLogBackup(log2Path, log1.EndTime!.Value.AddMinutes(5), log1.LastLSN!.Value);

            var target = log2.EndTime!.Value;
            var plan = RestorePlan.CreateValidPlan(
                "TestDB",
                target,
                full,
                differentialBackup: null,
                logBackups: new List<BackupJob> { log2, log1 },
                actualRestorePoint: target);

            var first = _service.Validate(plan);
            var second = _service.Validate(plan);

            first.IsValid.Should().BeFalse();
            first.Errors.Should().NotBeEmpty();
            first.Errors.Should().Contain(e => e.Contains("chronological order", StringComparison.OrdinalIgnoreCase));
            second.IsValid.Should().Be(first.IsValid);
            second.Errors.Should().BeEquivalentTo(first.Errors);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void SetBackupPath(BackupJob backupJob, string? path)
    {
        var field = typeof(BackupJob).GetField("<BackupFilePath>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        field!.SetValue(backupJob, path);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deadpool-restore-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTempFile(string directoryPath, string fileName)
    {
        var path = Path.Combine(directoryPath, fileName);
        File.WriteAllText(path, "test");
        return path;
    }

    private static BackupJob CreateCompletedFullBackup(string path, DateTime startTime)
    {
        var endTime = startTime.AddMinutes(20);
        var checkpointLsn = 2000m + (startTime.Ticks / TimeSpan.TicksPerHour);

        return BackupJob.Restore(
            "TestDB",
            BackupType.Full,
            BackupStatus.Completed,
            startTime,
            endTime,
            path,
            1024 * 1024 * 100,
            null,
            checkpointLsn - 50m,
            checkpointLsn + 50m,
            null,
            checkpointLsn);
    }

    private static BackupJob CreateCompletedDifferentialBackup(string path, DateTime startTime, decimal baseFullLsn, DateTime minStart)
    {
        if (startTime < minStart)
            startTime = minStart;

        var endTime = startTime.AddMinutes(10);

        return BackupJob.Restore(
            "TestDB",
            BackupType.Differential,
            BackupStatus.Completed,
            startTime,
            endTime,
            path,
            1024 * 1024 * 40,
            null,
            baseFullLsn + 1m,
            baseFullLsn + 20m,
            baseFullLsn,
            null);
    }

    private static BackupJob CreateCompletedLogBackup(string path, DateTime startTime, decimal firstLsn)
    {
        var endTime = startTime.AddMinutes(30);
        var executionStartTime = startTime;

        return BackupJob.Restore(
            "TestDB",
            BackupType.TransactionLog,
            BackupStatus.Completed,
            startTime,
            endTime,
            path,
            1024 * 1024 * 10,
            null,
            firstLsn,
            firstLsn + 50m,
            null,
            null,
            executionStartTime);
    }
}
