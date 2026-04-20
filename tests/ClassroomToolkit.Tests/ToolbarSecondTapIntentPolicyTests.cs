using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarSecondTapIntentPolicyTests
{
    [Theory]
    [InlineData(false, true, "QuickColor", "None")]
    [InlineData(true, false, "QuickColor", "None")]
    [InlineData(true, true, "QuickColor", "QuickColor")]
    [InlineData(true, true, "Shape", "Shape")]
    public void Resolve_ShouldOnlyOpenSecondaryAction_WhenItemIsAlreadySelected_AndSupportsIt(
        bool alreadySelected,
        bool supportsSecondaryAction,
        string requestedTarget,
        string expected)
    {
        ToolbarSecondTapIntentPolicy.Resolve(
            alreadySelected,
            supportsSecondaryAction,
            Enum.Parse<ToolbarSecondTapTarget>(requestedTarget)).Should().Be(Enum.Parse<ToolbarSecondTapTarget>(expected));
    }
}
