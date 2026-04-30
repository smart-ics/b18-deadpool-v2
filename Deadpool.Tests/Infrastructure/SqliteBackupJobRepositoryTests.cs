using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadpool.Tests.Infrastructure;

public class SqliteBackupJobRepositoryTests : IDisposable
{
    private readonly string _databasePath;

    public SqliteBackupJobRepositoryTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"deadpool-tests-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task GetLastSuccessfulFullBackupAsync_ShouldReturnCompletedFullBackup()
    {
        var repository = new SqliteBackupJobRepository(_databasePath, NullLogger<SqliteBackupJobRepository>.Instance);
        var job = CreateCompletedFullBackup("HospitalDB", @"C:\Backups\HospitalDB_full.bak", 1024);

        await repository.CreateAsync(job);

        var lastFull = await repository.GetLastSuccessfulFullBackupAsync("HospitalDB");

        lastFull.Should().NotBeNull();
        lastFull!.DatabaseName.Should().Be("HospitalDB");
        lastFull.BackupType.Should().Be(BackupType.Full);
        lastFull.Status.Should().Be(BackupStatus.Completed);
        lastFull.BackupFilePath.Should().Be(@"C:\Backups\HospitalDB_full.bak");
    }

    [Fact]
    public async Task HasSuccessfulFullBackupAsync_ShouldReturnTrue_WhenCompletedFullBackupExists()
    {
        var repository = new SqliteBackupJobRepository(_databasePath, NullLogger<SqliteBackupJobRepository>.Instance);
        var job = CreateCompletedFullBackup("HospitalDB", @"C:\Backups\HospitalDB_full.bak", 2048);

        await repository.CreateAsync(job);

        var hasFull = await repository.HasSuccessfulFullBackupAsync("HospitalDB");

        hasFull.Should().BeTrue();
    }

    private static BackupJob CreateCompletedFullBackup(string databaseName, string filePath, long fileSizeBytes)
    {
        var job = new BackupJob(databaseName, BackupType.Full, filePath);
        job.MarkAsRunning();
        job.MarkAsCompleted(fileSizeBytes);
        return job;
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
