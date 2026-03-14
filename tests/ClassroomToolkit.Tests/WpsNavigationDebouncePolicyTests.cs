using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsNavigationDebouncePolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenTargetIsZero()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: null,
            BlockUntilUtc: PresentationRuntimeDefaults.UnsetTimestampUtc);

        var suppressed = WpsNavigationDebouncePolicy.ShouldSuppress(
            direction: 1,
            target: IntPtr.Zero,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenWithinBlockWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: null,
            BlockUntilUtc: nowUtc.AddMilliseconds(80));

        var suppressed = WpsNavigationDebouncePolicy.ShouldSuppress(
            direction: 1,
            target: (IntPtr)123,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenSameTargetAndCodeWithinDebounceWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc.AddMilliseconds(-60)),
            BlockUntilUtc: PresentationRuntimeDefaults.UnsetTimestampUtc);

        var suppressed = WpsNavigationDebouncePolicy.ShouldSuppress(
            direction: 1,
            target: (IntPtr)123,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenDifferentTarget()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc.AddMilliseconds(-60)),
            BlockUntilUtc: PresentationRuntimeDefaults.UnsetTimestampUtc);

        var suppressed = WpsNavigationDebouncePolicy.ShouldSuppress(
            direction: 1,
            target: (IntPtr)456,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void Remember_ShouldCaptureEventAndBlockUntil()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);

        var state = WpsNavigationDebouncePolicy.Remember(
            direction: -1,
            target: (IntPtr)999,
            nowUtc: nowUtc,
            debounceMs: 200);

        state.LastEvent.Should().Be(( -1, (IntPtr)999, nowUtc ));
        state.BlockUntilUtc.Should().Be(nowUtc.AddMilliseconds(200));
    }
}
