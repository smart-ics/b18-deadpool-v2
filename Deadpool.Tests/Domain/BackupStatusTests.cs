using Deadpool.Core.Domain.Enums;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class BackupStatusTests
{
    [Fact]
    public void BackupStatus_ShouldHaveCorrectValues()
    {
        ((int)BackupStatus.Pending).Should().Be(1);
        ((int)BackupStatus.Running).Should().Be(2);
        ((int)BackupStatus.Completed).Should().Be(3);
        ((int)BackupStatus.Failed).Should().Be(4);
    }

    [Fact]
    public void BackupStatus_ShouldHaveFourStatuses()
    {
        var values = Enum.GetValues<BackupStatus>();
        values.Should().HaveCount(4);
    }
}
