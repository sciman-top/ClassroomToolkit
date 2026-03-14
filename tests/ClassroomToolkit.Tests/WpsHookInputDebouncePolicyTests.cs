using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookInputDebouncePolicyTests
{
    [Fact]
    public void IsRecent_ShouldReturnFalse_WhenNotInitialized()
    {
        var nowUtc = new DateTime(2026, 3, 7, 5, 0, 0, DateTimeKind.Utc);

        var recent = WpsHookInputDebouncePolicy.IsRecent(
            PresentationRuntimeDefaults.UnsetTimestampUtc,
            nowUtc,
            debounceMs: 200);

        recent.Should().BeFalse();
    }

    [Fact]
    public void IsRecent_ShouldReturnTrue_WhenWithinDebounceWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 5, 0, 0, DateTimeKind.Utc);

        var recent = WpsHookInputDebouncePolicy.IsRecent(
            nowUtc.AddMilliseconds(-80),
            nowUtc,
            debounceMs: 200);

        recent.Should().BeTrue();
    }

    [Fact]
    public void IsRecent_ShouldReturnFalse_WhenOutsideDebounceWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 5, 0, 0, DateTimeKind.Utc);

        var recent = WpsHookInputDebouncePolicy.IsRecent(
            nowUtc.AddMilliseconds(-260),
            nowUtc,
            debounceMs: 200);

        recent.Should().BeFalse();
    }
}
