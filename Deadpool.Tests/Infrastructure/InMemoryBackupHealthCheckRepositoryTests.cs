using Deadpool.Core.Domain.Entities;
using Deadpool.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Infrastructure;

public class InMemoryBackupHealthCheckRepositoryTests
{
    [Fact]
    public async Task CleanupOldHealthChecks_ShouldRemoveOldEntries()
    {
        var repository = new InMemoryBackupHealthCheckRepository();

        var oldCheck = CreateHealthCheckWithTime("TestDB", DateTime.UtcNow.AddDays(-10));
        var recentCheck = CreateHealthCheckWithTime("TestDB", DateTime.UtcNow.AddDays(-2));

        await repository.CreateAsync(oldCheck);
        await repository.CreateAsync(recentCheck);

        repository.CleanupOldHealthChecks(TimeSpan.FromDays(7));

        var result = await repository.GetLatestHealthCheckAsync("TestDB");
        result.Should().NotBeNull();
        result!.CheckTime.Should().BeCloseTo(recentCheck.CheckTime, TimeSpan.FromSeconds(1));

        var allRecent = await repository.GetRecentHealthChecksAsync("TestDB", 10);
        allRecent.Should().HaveCount(1);
    }

    [Fact]
    public async Task CleanupOldHealthChecks_ShouldPreserveRecentEntries()
    {
        var repository = new InMemoryBackupHealthCheckRepository();

        var check1 = CreateHealthCheckWithTime("TestDB", DateTime.UtcNow.AddDays(-1));
        var check2 = CreateHealthCheckWithTime("TestDB", DateTime.UtcNow.AddHours(-12));

        await repository.CreateAsync(check1);
        await repository.CreateAsync(check2);

        repository.CleanupOldHealthChecks(TimeSpan.FromDays(7));

        var allRecent = await repository.GetRecentHealthChecksAsync("TestDB", 10);
        allRecent.Should().HaveCount(2);
    }

    [Fact]
    public async Task CleanupOldHealthChecks_ShouldHandleEmptyRepository()
    {
        var repository = new InMemoryBackupHealthCheckRepository();

        var action = () => repository.CleanupOldHealthChecks(TimeSpan.FromDays(7));

        action.Should().NotThrow();
    }

    [Fact]
    public void GetHealthCheckCount_ShouldReturnCorrectCount()
    {
        var repository = new InMemoryBackupHealthCheckRepository();

        repository.GetHealthCheckCount().Should().Be(0);
    }

    [Fact]
    public async Task GetHealthCheckCount_ShouldIncreaseAfterCreate()
    {
        var repository = new InMemoryBackupHealthCheckRepository();

        var check = new BackupHealthCheck("TestDB");
        await repository.CreateAsync(check);

        repository.GetHealthCheckCount().Should().Be(1);
    }

    [Fact]
    public async Task GetHealthCheckCount_ShouldDecreaseAfterCleanup()
    {
        var repository = new InMemoryBackupHealthCheckRepository();

        var oldCheck = CreateHealthCheckWithTime("TestDB", DateTime.UtcNow.AddDays(-10));
        await repository.CreateAsync(oldCheck);

        repository.GetHealthCheckCount().Should().Be(1);

        repository.CleanupOldHealthChecks(TimeSpan.FromDays(7));

        repository.GetHealthCheckCount().Should().Be(0);
    }

    private BackupHealthCheck CreateHealthCheckWithTime(string databaseName, DateTime checkTime)
    {
        var healthCheck = new BackupHealthCheck(databaseName);

        typeof(BackupHealthCheck)
            .GetField("<CheckTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(healthCheck, checkTime);

        return healthCheck;
    }
}
