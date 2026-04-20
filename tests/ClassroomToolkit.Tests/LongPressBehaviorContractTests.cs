using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LongPressBehaviorContractTests
{
    [Fact]
    public void LongPressBehavior_ShouldRegisterTouchEvents_AndGuardAgainstPromotedMouseReentry()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("element.PreviewTouchDown += OnTouchDown;");
        source.Should().Contain("element.PreviewTouchUp += OnTouchUp;");
        source.Should().Contain("element.LostTouchCapture += OnTouchLostCapture;");
        source.Should().Contain("private static void OnMouseLeave");
        source.Should().Contain("if (ShouldIgnoreMousePromotion(element))");
        source.Should().Contain("SetTouchPressActive(element, isActive: true);");
        source.Should().Contain("SetTouchPressActive(element, isActive: false);");
        source.Should().Contain("MarkMousePromotionSuppressed(element);");
        source.Should().Contain("StartPressTimer(element);");
        source.Should().Contain("StopPressTimer(element);");
        source.Should().Contain("CompletePress(element);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Behaviors",
            "LongPressBehavior.cs");
    }
}
