using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowOnClosingPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowClose_WhenCloseAlreadyAllowed()
    {
        var plan = MainWindowOnClosingPlanPolicy.Resolve(allowClose: true);

        plan.ShouldCancelClose.Should().BeFalse();
        plan.ShouldRequestExit.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldCancelAndRequestExit_WhenCloseNotAllowed()
    {
        var plan = MainWindowOnClosingPlanPolicy.Resolve(allowClose: false);

        plan.ShouldCancelClose.Should().BeTrue();
        plan.ShouldRequestExit.Should().BeTrue();
    }
}
