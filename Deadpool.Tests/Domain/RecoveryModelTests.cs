using Deadpool.Core.Domain.Enums;
using FluentAssertions;

namespace Deadpool.Tests.Domain;

public class RecoveryModelTests
{
    [Fact]
    public void RecoveryModel_ShouldHaveCorrectValues()
    {
        ((int)RecoveryModel.Simple).Should().Be(1);
        ((int)RecoveryModel.Full).Should().Be(2);
        ((int)RecoveryModel.BulkLogged).Should().Be(3);
    }

    [Fact]
    public void RecoveryModel_ShouldHaveThreeModels()
    {
        var values = Enum.GetValues<RecoveryModel>();
        values.Should().HaveCount(3);
    }
}
