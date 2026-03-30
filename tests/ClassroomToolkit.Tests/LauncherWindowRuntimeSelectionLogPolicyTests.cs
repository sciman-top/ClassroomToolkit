using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherWindowRuntimeSelectionLogPolicyTests
{
    [Theory]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible)]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible)]
    public void ShouldLog_ShouldReturnTrue_ForFallbackReasons(int reasonValue)
    {
        var reason = (LauncherWindowRuntimeSelectionReason)reasonValue;
        LauncherWindowRuntimeSelectionLogPolicy.ShouldLog(reason).Should().BeTrue();
    }

    [Theory]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.PreferMainVisible)]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.PreferBubbleVisible)]
    [InlineData((int)LauncherWindowRuntimeSelectionReason.None)]
    public void ShouldLog_ShouldReturnFalse_ForNonFallbackReasons(int reasonValue)
    {
        var reason = (LauncherWindowRuntimeSelectionReason)reasonValue;
        LauncherWindowRuntimeSelectionLogPolicy.ShouldLog(reason).Should().BeFalse();
    }
}
