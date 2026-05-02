using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Services;
using FluentAssertions;

namespace Deadpool.Tests.Services;

public class RestoreSafetyGuardServiceTests
{
    private readonly RestoreSafetyGuardService _sut = new();

    [Fact]
    public void EnsureConfirmed_WhenConfirmedIsFalse_ThrowsWithOverwriteWarning()
    {
        var context = new RestoreConfirmationContext
        {
            DatabaseName = "TestDB",
            Confirmed = false,
            RequireTextMatch = false
        };

        Action act = () => _sut.EnsureConfirmed(context);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Restore not confirmed*This operation will overwrite database 'TestDB'*");
    }

    [Fact]
    public void EnsureConfirmed_WhenDatabaseNameMissing_Throws()
    {
        var context = new RestoreConfirmationContext
        {
            DatabaseName = "",
            Confirmed = true,
            RequireTextMatch = false
        };

        Action act = () => _sut.EnsureConfirmed(context);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Target database must be specified*overwrite database*<unknown>*");
    }

    [Fact]
    public void EnsureConfirmed_WhenRequireTextMatchAndMismatch_Throws()
    {
        var context = new RestoreConfirmationContext
        {
            DatabaseName = "TestDB",
            Confirmed = true,
            ConfirmationText = "testdb",
            RequireTextMatch = true
        };

        Action act = () => _sut.EnsureConfirmed(context);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*confirmation text mismatch*exact database name 'TestDB'*overwrite database 'TestDB'*");
    }

    [Fact]
    public void EnsureConfirmed_WhenRequireTextMatchAndExactMatch_DoesNotThrow()
    {
        var context = new RestoreConfirmationContext
        {
            DatabaseName = "TestDB",
            Confirmed = true,
            ConfirmationText = "TestDB",
            RequireTextMatch = true
        };

        var act = () => _sut.EnsureConfirmed(context);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureConfirmed_WhenStrictModeDisabled_DoesNotRequireText()
    {
        var context = new RestoreConfirmationContext
        {
            DatabaseName = "TestDB",
            Confirmed = true,
            ConfirmationText = null,
            RequireTextMatch = false
        };

        var act = () => _sut.EnsureConfirmed(context);

        act.Should().NotThrow();
    }
}
