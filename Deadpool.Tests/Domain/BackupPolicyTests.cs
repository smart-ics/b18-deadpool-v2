using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class BackupPolicyTests
{
    private readonly BackupSchedule _fullSchedule;
    private readonly BackupSchedule _diffSchedule;
    private readonly BackupSchedule _logSchedule;

    public BackupPolicyTests()
    {
        _fullSchedule = new BackupSchedule("0 2 * * 0");
        _diffSchedule = new BackupSchedule("0 3 * * *");
        _logSchedule = new BackupSchedule("0 */2 * * *");
    }

    [Fact]
    public void Constructor_ShouldCreateBackupPolicy_WhenValidParametersWithFullRecoveryModel()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        policy.DatabaseName.Should().Be("MyHospitalDB");
        policy.RecoveryModel.Should().Be(RecoveryModel.Full);
        policy.FullBackupSchedule.Should().Be(_fullSchedule);
        policy.DifferentialBackupSchedule.Should().Be(_diffSchedule);
        policy.TransactionLogBackupSchedule.Should().Be(_logSchedule);
        policy.RetentionDays.Should().Be(30);
    }

    [Fact]
    public void Constructor_ShouldCreateBackupPolicy_WhenSimpleRecoveryModelWithoutLogSchedule()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Simple,
            _fullSchedule,
            _diffSchedule,
            null,
            30);

        policy.DatabaseName.Should().Be("MyHospitalDB");
        policy.RecoveryModel.Should().Be(RecoveryModel.Simple);
        policy.TransactionLogBackupSchedule.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenDatabaseNameEmpty(string databaseName)
    {
        var act = () => new BackupPolicy(
            databaseName,
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Database name cannot be empty.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ShouldThrowArgumentException_WhenRetentionDaysInvalid(int retentionDays)
    {
        var act = () => new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            retentionDays);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Retention days must be greater than zero.*");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenFullScheduleNull()
    {
        var act = () => new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            null!,
            _diffSchedule,
            _logSchedule,
            30);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fullBackupSchedule");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenDifferentialScheduleNull()
    {
        var act = () => new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            null!,
            _logSchedule,
            30);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("differentialBackupSchedule");
    }

    [Fact]
    public void Constructor_ShouldThrowInvalidOperationException_WhenSimpleRecoveryModelWithLogSchedule()
    {
        var act = () => new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Simple,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Transaction log backup is not supported for recovery model 'Simple'*");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenFullRecoveryModelWithoutLogSchedule()
    {
        var act = () => new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            null,
            30);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Transaction log backup schedule is required for recovery model 'Full'*");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenBulkLoggedRecoveryModelWithoutLogSchedule()
    {
        var act = () => new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.BulkLogged,
            _fullSchedule,
            _diffSchedule,
            null,
            30);

        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Transaction log backup schedule is required for recovery model 'BulkLogged'*");
    }

    [Fact]
    public void SupportsTransactionLogBackup_ShouldReturnTrue_WhenFullRecoveryModel()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        policy.SupportsTransactionLogBackup().Should().BeTrue();
    }

    [Fact]
    public void SupportsTransactionLogBackup_ShouldReturnTrue_WhenBulkLoggedRecoveryModel()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.BulkLogged,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        policy.SupportsTransactionLogBackup().Should().BeTrue();
    }

    [Fact]
    public void SupportsTransactionLogBackup_ShouldReturnFalse_WhenSimpleRecoveryModel()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Simple,
            _fullSchedule,
            _diffSchedule,
            null,
            30);

        policy.SupportsTransactionLogBackup().Should().BeFalse();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnFalse_WhenBackupWithinRetentionPeriod()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-15);

        var canDelete = policy.CanDeleteBackup(BackupType.Full, backupDate, null);

        canDelete.Should().BeFalse();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnFalse_WhenNoLastFullBackupDateAvailable()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.Full, backupDate, null);

        canDelete.Should().BeFalse();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnFalse_WhenFullBackupIsTheLastFull()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var lastFullBackupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.Full, lastFullBackupDate, lastFullBackupDate);

        canDelete.Should().BeFalse();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnFalse_WhenFullBackupIsNewerThanLastFull()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-40);
        var lastFullBackupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.Full, backupDate, lastFullBackupDate);

        canDelete.Should().BeFalse();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnTrue_WhenFullBackupIsOlderThanLastFull()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-60);
        var lastFullBackupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.Full, backupDate, lastFullBackupDate);

        canDelete.Should().BeTrue();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnFalse_WhenDifferentialIsPartOfCurrentRestoreChain()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-40);
        var lastFullBackupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.Differential, backupDate, lastFullBackupDate);

        canDelete.Should().BeFalse();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnTrue_WhenDifferentialIsBeforeLastFullBackup()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-60);
        var lastFullBackupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.Differential, backupDate, lastFullBackupDate);

        canDelete.Should().BeTrue();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnFalse_WhenLogBackupIsPartOfCurrentRestoreChain()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-40);
        var lastFullBackupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.TransactionLog, backupDate, lastFullBackupDate);

        canDelete.Should().BeFalse();
    }

    [Fact]
    public void CanDeleteBackup_ShouldReturnTrue_WhenLogBackupIsBeforeLastFullBackup()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var backupDate = DateTime.UtcNow.AddDays(-60);
        var lastFullBackupDate = DateTime.UtcNow.AddDays(-45);

        var canDelete = policy.CanDeleteBackup(BackupType.TransactionLog, backupDate, lastFullBackupDate);

        canDelete.Should().BeTrue();
    }

    [Fact]
    public void Properties_ShouldBeImmutable_WhenPolicyCreated()
    {
        var policy = new BackupPolicy(
            "MyHospitalDB",
            RecoveryModel.Full,
            _fullSchedule,
            _diffSchedule,
            _logSchedule,
            30);

        var databaseNameProperty = typeof(BackupPolicy).GetProperty(nameof(BackupPolicy.DatabaseName));
        var recoveryModelProperty = typeof(BackupPolicy).GetProperty(nameof(BackupPolicy.RecoveryModel));
        var retentionDaysProperty = typeof(BackupPolicy).GetProperty(nameof(BackupPolicy.RetentionDays));

        databaseNameProperty!.SetMethod.Should().BeNull("DatabaseName should be immutable");
        recoveryModelProperty!.SetMethod.Should().BeNull("RecoveryModel should be immutable");
        retentionDaysProperty!.SetMethod.Should().BeNull("RetentionDays should be immutable");
    }
}
