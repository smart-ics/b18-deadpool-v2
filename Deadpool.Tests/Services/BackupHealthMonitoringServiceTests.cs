using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Services;
using Deadpool.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Services;

public class BackupHealthMonitoringServiceTests
{
    private readonly InMemoryBackupJobRepository _repository;
    private readonly BackupHealthOptions _options;
    private readonly BackupHealthMonitoringService _service;

    public BackupHealthMonitoringServiceTests()
    {
        _repository = new InMemoryBackupJobRepository();
        _options = new BackupHealthOptions(
            fullBackupOverdueThreshold: TimeSpan.FromHours(24),
            differentialBackupOverdueThreshold: TimeSpan.FromHours(6),
            logBackupOverdueThreshold: TimeSpan.FromMinutes(30),
            chainLookbackPeriod: TimeSpan.FromDays(7)
        );
        _service = new BackupHealthMonitoringService(_repository, _options);
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldReturnHealthy_WhenAllBackupsRecent()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var diffBackup = CreateCompletedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-2));
        var logBackup = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddMinutes(-10));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(diffBackup);
        await _repository.CreateAsync(logBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.IsHealthy().Should().BeTrue();
        result.LastSuccessfulFullBackup.Should().NotBeNull();
        result.LastSuccessfulDifferentialBackup.Should().NotBeNull();
        result.LastSuccessfulLogBackup.Should().NotBeNull();
        result.Warnings.Should().BeEmpty();
        result.CriticalFindings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldReturnCritical_WhenNoFullBackup()
    {
        var policy = CreateFullRecoveryPolicy();

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.IsCritical().Should().BeTrue();
        result.CriticalFindings.Should().Contain(f => f.Contains("No successful full backup found"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldReturnWarning_WhenFullBackupOverdue()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-30));
        await _repository.CreateAsync(fullBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.HasWarnings().Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Full backup overdue"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldReturnWarning_WhenDifferentialBackupOverdue()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var diffBackup = CreateCompletedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-8));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(diffBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.HasWarnings().Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Differential backup overdue"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldReturnWarning_WhenLogBackupOverdue()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var logBackup = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddMinutes(-45));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(logBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.HasWarnings().Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Transaction log backup overdue"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldReturnWarning_WhenNoLogBackupFound()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        await _repository.CreateAsync(fullBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.HasWarnings().Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("No successful transaction log backup found"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldNotCheckLogBackups_WhenSimpleRecoveryModel()
    {
        var policy = CreateSimpleRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        await _repository.CreateAsync(fullBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.IsHealthy().Should().BeTrue();
        result.LastSuccessfulLogBackup.Should().BeNull();
        result.Warnings.Should().NotContain(w => w.Contains("log"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldDetectMissingFullBackupInChain()
    {
        var policy = CreateFullRecoveryPolicy();

        var diffBackup = CreateCompletedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-2));
        var logBackup = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddMinutes(-10));

        await _repository.CreateAsync(diffBackup);
        await _repository.CreateAsync(logBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.IsCritical().Should().BeTrue();
        result.CriticalFindings.Should().Contain(f => f.Contains("No full backup in chain"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldDetectLargeGapInLogChain()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var logBackup1 = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddHours(-6));
        var logBackup2 = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddMinutes(-10));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(logBackup1);
        await _repository.CreateAsync(logBackup2);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.HasWarnings().Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Large gap in log backup chain"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldWarn_WhenNoLogsAfterDifferential()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var logBackup1 = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddHours(-6));
        var diffBackup = CreateCompletedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-2));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(logBackup1);
        await _repository.CreateAsync(diffBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.HasWarnings().Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("No log backups after last differential"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldRecordLastFailedBackup()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var failedBackup = CreateFailedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-1));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(failedBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.LastFailedBackup.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CheckDatabaseHealthAsync_ShouldThrow_WhenDatabaseNameInvalid(string databaseName)
    {
        var policy = CreateFullRecoveryPolicy();

        var act = async () => await _service.CheckDatabaseHealthAsync(databaseName, policy);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldThrow_WhenPolicyIsNull()
    {
        var act = async () => await _service.CheckDatabaseHealthAsync("TestDB", null);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private BackupJob CreateCompletedJob(string databaseName, BackupType backupType, DateTime startTime)
    {
        // Use non-rooted path so file existence check will skip it
        var job = new BackupJob(databaseName, backupType, $"backup/{databaseName}_{backupType}.bak");

        typeof(BackupJob)
            .GetField("<StartTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, startTime);

        job.MarkAsRunning();
        job.MarkAsCompleted(1024);

        // Set EndTime to match startTime so age calculation works correctly
        typeof(BackupJob)
            .GetField("<EndTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, startTime.AddMinutes(5));

        return job;
    }

    private BackupJob CreateFailedJob(string databaseName, BackupType backupType, DateTime startTime)
    {
        // Use non-rooted path so file existence check will skip it
        var job = new BackupJob(databaseName, backupType, $"backup/{databaseName}_{backupType}.bak");

        typeof(BackupJob)
            .GetField("<StartTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, startTime);

        job.MarkAsRunning();
        job.MarkAsFailed("Test error");

        // Set EndTime to match startTime so age calculation works correctly
        typeof(BackupJob)
            .GetField("<EndTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, startTime.AddMinutes(2));

        return job;
    }

    private BackupPolicy CreateFullRecoveryPolicy()
    {
        return new BackupPolicy(
            databaseName: "TestDB",
            recoveryModel: RecoveryModel.Full,
            fullBackupSchedule: new BackupSchedule("0 0 * * *"),
            differentialBackupSchedule: new BackupSchedule("0 */6 * * *"),
            transactionLogBackupSchedule: new BackupSchedule("*/15 * * * *"),
            retentionDays: 7
        );
    }

    private BackupPolicy CreateSimpleRecoveryPolicy()
    {
        return new BackupPolicy(
            databaseName: "TestDB",
            recoveryModel: RecoveryModel.Simple,
            fullBackupSchedule: new BackupSchedule("0 0 * * *"),
            differentialBackupSchedule: new BackupSchedule("0 */6 * * *"),
            transactionLogBackupSchedule: null,
            retentionDays: 7
        );
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldAlwaysIncludeLimitations()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        await _repository.CreateAsync(fullBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.Limitations.Should().NotBeEmpty();
        result.Limitations.Should().Contain(l => l.Contains("LSN"));
        result.Limitations.Should().Contain(l => l.Contains("Differential base"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldUsesCompletionTimeForFreshness()
    {
        var policy = CreateFullRecoveryPolicy();

        var startTime = DateTime.UtcNow.AddHours(-30);
        var endTime = DateTime.UtcNow.AddHours(-12);

        var fullBackup = CreateCompletedJobWithEndTime("TestDB", BackupType.Full, startTime, endTime);
        await _repository.CreateAsync(fullBackup);

        // Add recent differential and log backups so the test focuses on completion time usage
        var diffBackup = CreateCompletedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-2));
        var logBackup = CreateCompletedJob("TestDB", BackupType.TransactionLog, DateTime.UtcNow.AddMinutes(-10));
        await _repository.CreateAsync(diffBackup);
        await _repository.CreateAsync(logBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.LastSuccessfulFullBackup.Should().Be(endTime);
        result.IsHealthy().Should().BeTrue();
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldWarnOnRecentFailure()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var failedBackup = CreateFailedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-2));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(failedBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.HasWarnings().Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Recent backup failure"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldNotWarnOnOldFailure()
    {
        var policy = CreateFullRecoveryPolicy();

        var fullBackup = CreateCompletedJob("TestDB", BackupType.Full, DateTime.UtcNow.AddHours(-12));
        var failedBackup = CreateFailedJob("TestDB", BackupType.Differential, DateTime.UtcNow.AddHours(-30));

        await _repository.CreateAsync(fullBackup);
        await _repository.CreateAsync(failedBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.Warnings.Should().NotContain(w => w.Contains("Recent backup failure"));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_ShouldRaiseCriticalIfBackupFileMissing()
    {
        var policy = CreateFullRecoveryPolicy();

        // Use Windows path that will be checked and found missing
        var fullBackup = new BackupJob("TestDB", BackupType.Full, @"C:\NonExistent\TestDB_Full.bak");

        typeof(BackupJob)
            .GetField("<StartTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(fullBackup, DateTime.UtcNow.AddHours(-12));

        fullBackup.MarkAsRunning();
        fullBackup.MarkAsCompleted(1024);

        typeof(BackupJob)
            .GetField("<EndTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(fullBackup, DateTime.UtcNow.AddHours(-12).AddMinutes(5));

        await _repository.CreateAsync(fullBackup);

        var result = await _service.CheckDatabaseHealthAsync("TestDB", policy);

        result.IsCritical().Should().BeTrue();
        result.CriticalFindings.Should().Contain(f => f.Contains("backup file missing"));
    }

    private BackupJob CreateCompletedJobWithEndTime(string databaseName, BackupType backupType, DateTime startTime, DateTime endTime)
    {
        // Use non-rooted path so file existence check will skip it
        var job = new BackupJob(databaseName, backupType, $"backup/{databaseName}_{backupType}.bak");

        typeof(BackupJob)
            .GetField("<StartTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, startTime);

        job.MarkAsRunning();
        job.MarkAsCompleted(1024);

        typeof(BackupJob)
            .GetField("<EndTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(job, endTime);

        return job;
    }
}
