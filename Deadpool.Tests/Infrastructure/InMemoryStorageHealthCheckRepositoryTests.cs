using Deadpool.Core.Domain.Entities;
using Deadpool.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Deadpool.Tests.Infrastructure;

public class InMemoryStorageHealthCheckRepositoryTests
{
    [Fact]
    public async Task CreateAsync_ShouldStoreHealthCheck()
    {
        var repository = new InMemoryStorageHealthCheckRepository();
        var healthCheck = new StorageHealthCheck("C:\\Backups");

        await repository.CreateAsync(healthCheck);

        var latest = await repository.GetLatestHealthCheckAsync("C:\\Backups");
        latest.Should().NotBeNull();
        latest!.VolumePath.Should().Be("C:\\Backups");
    }

    [Fact]
    public async Task GetLatestHealthCheckAsync_ShouldReturnMostRecent()
    {
        var repository = new InMemoryStorageHealthCheckRepository();
        var check1 = new StorageHealthCheck("C:\\Backups");
        await Task.Delay(10);
        var check2 = new StorageHealthCheck("C:\\Backups");

        await repository.CreateAsync(check1);
        await repository.CreateAsync(check2);

        var latest = await repository.GetLatestHealthCheckAsync("C:\\Backups");
        latest.Should().BeSameAs(check2);
    }

    [Fact]
    public async Task GetLatestHealthCheckAsync_ShouldReturnNull_WhenNoChecks()
    {
        var repository = new InMemoryStorageHealthCheckRepository();

        var latest = await repository.GetLatestHealthCheckAsync("C:\\Backups");

        latest.Should().BeNull();
    }

    [Fact]
    public async Task GetRecentHealthChecksAsync_ShouldReturnLimitedResults()
    {
        var repository = new InMemoryStorageHealthCheckRepository();
        for (int i = 0; i < 5; i++)
        {
            await repository.CreateAsync(new StorageHealthCheck("C:\\Backups"));
            await Task.Delay(10);
        }

        var recent = await repository.GetRecentHealthChecksAsync("C:\\Backups", 3);

        recent.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRecentHealthChecksAsync_ShouldReturnInDescendingOrder()
    {
        var repository = new InMemoryStorageHealthCheckRepository();
        var check1 = new StorageHealthCheck("C:\\Backups");
        await Task.Delay(10);
        var check2 = new StorageHealthCheck("C:\\Backups");
        await Task.Delay(10);
        var check3 = new StorageHealthCheck("C:\\Backups");

        await repository.CreateAsync(check1);
        await repository.CreateAsync(check2);
        await repository.CreateAsync(check3);

        var recent = (await repository.GetRecentHealthChecksAsync("C:\\Backups", 3)).ToList();

        recent[0].Should().BeSameAs(check3);
        recent[1].Should().BeSameAs(check2);
        recent[2].Should().BeSameAs(check1);
    }

    [Fact]
    public void CleanupOldHealthChecks_ShouldRemoveOldEntries()
    {
        var repository = new InMemoryStorageHealthCheckRepository();
        var oldCheck = new StorageHealthCheck("C:\\Backups");

        typeof(StorageHealthCheck)
            .GetField("<CheckTime>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(oldCheck, DateTime.UtcNow.AddDays(-10));

        var recentCheck = new StorageHealthCheck("C:\\Backups");

        repository.CreateAsync(oldCheck).Wait();
        repository.CreateAsync(recentCheck).Wait();

        repository.CleanupOldHealthChecks(TimeSpan.FromDays(7));

        repository.GetHealthCheckCount().Should().Be(1);
    }

    [Fact]
    public void CleanupOldHealthChecks_ShouldPreserveRecentEntries()
    {
        var repository = new InMemoryStorageHealthCheckRepository();
        var check1 = new StorageHealthCheck("C:\\Backups");
        var check2 = new StorageHealthCheck("D:\\Backups");

        repository.CreateAsync(check1).Wait();
        repository.CreateAsync(check2).Wait();

        repository.CleanupOldHealthChecks(TimeSpan.FromDays(1));

        repository.GetHealthCheckCount().Should().Be(2);
    }

    [Fact]
    public void CleanupOldHealthChecks_ShouldHandleEmptyRepository()
    {
        var repository = new InMemoryStorageHealthCheckRepository();

        var act = () => repository.CleanupOldHealthChecks(TimeSpan.FromDays(7));

        act.Should().NotThrow();
        repository.GetHealthCheckCount().Should().Be(0);
    }

    [Fact]
    public void GetHealthCheckCount_ShouldReturnCorrectCount()
    {
        var repository = new InMemoryStorageHealthCheckRepository();

        repository.GetHealthCheckCount().Should().Be(0);

        repository.CreateAsync(new StorageHealthCheck("C:\\Backups")).Wait();
        repository.GetHealthCheckCount().Should().Be(1);

        repository.CreateAsync(new StorageHealthCheck("D:\\Backups")).Wait();
        repository.GetHealthCheckCount().Should().Be(2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var repository = new InMemoryStorageHealthCheckRepository();
        repository.CreateAsync(new StorageHealthCheck("C:\\Backups")).Wait();
        repository.CreateAsync(new StorageHealthCheck("D:\\Backups")).Wait();

        repository.Clear();

        repository.GetHealthCheckCount().Should().Be(0);
    }
}
