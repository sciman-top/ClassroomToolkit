using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationNavigationOrchestratorTests
{
    [Fact]
    public void TryParseHook_ShouldMapUnknownSourceToKeyboard()
    {
        var parsed = PresentationNavigationIntentParser.TryParseHook(
            direction: 1,
            source: "unknown",
            out var intent);

        parsed.Should().BeTrue();
        intent.Direction.Should().Be(1);
        intent.Source.Should().Be(PresentationNavigationSource.HookKeyboard);
    }

    [Fact]
    public void TryParseHook_ShouldReturnFalse_WhenDirectionIsZero()
    {
        var parsed = PresentationNavigationIntentParser.TryParseHook(
            direction: 0,
            source: "keyboard",
            out var intent);

        parsed.Should().BeFalse();
        intent.Should().Be(PresentationNavigationIntent.None);
    }

    [Fact]
    public void ResolveHook_ShouldBlock_WhenWheelSuppressedByRecentInk()
    {
        var intent = new PresentationNavigationIntent(1, PresentationNavigationSource.HookWheel);

        var result = PresentationNavigationOrchestrator.ResolveHook(
            intent,
            suppressWheelFromRecentInkInput: true,
            targetValid: true,
            passthrough: false,
            interceptSource: true,
            suppressedAsDebounced: false);

        result.ShouldDispatch.Should().BeFalse();
        result.BlockReason.Should().Be(PresentationNavigationBlockReason.WheelSuppressedByRecentInkInput);
    }

    [Fact]
    public void ResolveHook_ShouldBlock_WhenTargetInvalid()
    {
        var intent = new PresentationNavigationIntent(1, PresentationNavigationSource.HookKeyboard);

        var result = PresentationNavigationOrchestrator.ResolveHook(
            intent,
            suppressWheelFromRecentInkInput: false,
            targetValid: false,
            passthrough: false,
            interceptSource: true,
            suppressedAsDebounced: false);

        result.ShouldDispatch.Should().BeFalse();
        result.BlockReason.Should().Be(PresentationNavigationBlockReason.TargetInvalid);
    }

    [Fact]
    public void ResolveHook_ShouldBlock_WhenPassthroughAndInterceptDisabled()
    {
        var intent = new PresentationNavigationIntent(1, PresentationNavigationSource.HookKeyboard);

        var result = PresentationNavigationOrchestrator.ResolveHook(
            intent,
            suppressWheelFromRecentInkInput: false,
            targetValid: true,
            passthrough: true,
            interceptSource: false,
            suppressedAsDebounced: false);

        result.ShouldDispatch.Should().BeFalse();
        result.BlockReason.Should().Be(PresentationNavigationBlockReason.RawPassthroughWithoutIntercept);
    }

    [Fact]
    public void ResolveHook_ShouldBlock_WhenDebounced()
    {
        var intent = new PresentationNavigationIntent(-1, PresentationNavigationSource.HookKeyboard);

        var result = PresentationNavigationOrchestrator.ResolveHook(
            intent,
            suppressWheelFromRecentInkInput: false,
            targetValid: true,
            passthrough: false,
            interceptSource: true,
            suppressedAsDebounced: true);

        result.ShouldDispatch.Should().BeFalse();
        result.BlockReason.Should().Be(PresentationNavigationBlockReason.Debounced);
    }

    [Fact]
    public void ResolveHook_ShouldDispatchNext_WhenPositiveDirection()
    {
        var intent = new PresentationNavigationIntent(9, PresentationNavigationSource.HookKeyboard);

        var result = PresentationNavigationOrchestrator.ResolveHook(
            intent,
            suppressWheelFromRecentInkInput: false,
            targetValid: true,
            passthrough: false,
            interceptSource: true,
            suppressedAsDebounced: false);

        result.ShouldDispatch.Should().BeTrue();
        result.DirectionCode.Should().Be(1);
        result.BlockReason.Should().Be(PresentationNavigationBlockReason.None);
    }

    [Fact]
    public void ResolveHook_ShouldDispatchPrevious_WhenNegativeDirection()
    {
        var intent = new PresentationNavigationIntent(-9, PresentationNavigationSource.HookKeyboard);

        var result = PresentationNavigationOrchestrator.ResolveHook(
            intent,
            suppressWheelFromRecentInkInput: false,
            targetValid: true,
            passthrough: false,
            interceptSource: true,
            suppressedAsDebounced: false);

        result.ShouldDispatch.Should().BeTrue();
        result.DirectionCode.Should().Be(-1);
        result.BlockReason.Should().Be(PresentationNavigationBlockReason.None);
    }
}
