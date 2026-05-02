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

    [Fact]
    public async Task UpdateAsync_ShouldPersistNewBackupFilePath_WhenJobCompletes()
    {
        var repository = new SqliteBackupJobRepository(_databasePath, NullLogger<SqliteBackupJobRepository>.Instance);
        var job = new BackupJob("HospitalDB", BackupType.Differential, "PENDING_HospitalDB_DIFF_20260502_0025.bak");

        await repository.CreateAsync(job);

        var claimed = await repository.TryClaimJobAsync(job);
        claimed.Should().BeTrue();

        job.MarkAsRunning();
        var actualPath = @"C:\Backups\HospitalDB_DIFF_20260502_0025.bak";
        job.MarkAsCompleted(actualPath, 1024);

        await repository.UpdateAsync(job);

        var backups = await repository.GetBackupsByDatabaseAsync("HospitalDB");
        var persisted = backups.Single();

        persisted.Status.Should().Be(BackupStatus.Completed);
        persisted.BackupFilePath.Should().Be(actualPath);
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
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Delete(_databasePath);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(50);
                }
                catch (IOException)
                {
                    break;
                }
            }
        }
    }
}
