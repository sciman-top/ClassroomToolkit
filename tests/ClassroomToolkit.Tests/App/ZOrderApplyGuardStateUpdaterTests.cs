using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderApplyGuardStateUpdaterTests
{
    [Fact]
    public void TryEnter_ShouldSetApplying_WhenNotApplying()
    {
        var applying = false;

        var entered = ZOrderApplyGuardStateUpdater.TryEnter(ref applying);

        entered.Should().BeTrue();
        applying.Should().BeTrue();
    }

    [Fact]
    public void TryEnter_ShouldReturnFalse_WhenAlreadyApplying()
    {
        var applying = true;

        var entered = ZOrderApplyGuardStateUpdater.TryEnter(ref applying);

        entered.Should().BeFalse();
        applying.Should().BeTrue();
    }

    [Fact]
    public void Exit_ShouldClearApplying()
    {
        var applying = true;

        ZOrderApplyGuardStateUpdater.Exit(ref applying);

        applying.Should().BeFalse();
    }
}
