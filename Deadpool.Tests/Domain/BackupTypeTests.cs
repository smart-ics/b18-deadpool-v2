using Deadpool.Core.Domain.Enums;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class BackupTypeTests
{
    [Fact]
    public void BackupType_ShouldHaveCorrectValues()
    {
        ((int)BackupType.Full).Should().Be(1);
        ((int)BackupType.Differential).Should().Be(2);
        ((int)BackupType.TransactionLog).Should().Be(3);
    }

    [Fact]
    public void BackupType_ShouldHaveThreeTypes()
    {
        var values = Enum.GetValues<BackupType>();
        values.Should().HaveCount(3);
    }
}
