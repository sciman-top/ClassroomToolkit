using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageRegionEraseNavigationPolicyTests
{
    [Fact]
    public void Resolve_ShouldUseStableNavigationPath()
    {
        var plan = CrossPageRegionEraseNavigationPolicy.Resolve();

        plan.InteractiveSwitch.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }
}
