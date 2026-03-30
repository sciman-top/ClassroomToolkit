using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageSwitchBitmapResolverTests
{
    [Fact]
    public void ResolveForInteractiveSwitch_ShouldReusePreloadedBitmap_AndSkipLoader()
    {
        var preloaded = new object();
        var loaderCalled = false;

        var resolved = CrossPageSwitchBitmapResolver.ResolveForInteractiveSwitch(
            interactiveSwitch: true,
            preloadedBitmap: preloaded,
            loadBitmap: () =>
            {
                loaderCalled = true;
                return new object();
            });

        resolved.Should().BeSameAs(preloaded);
        loaderCalled.Should().BeFalse();
    }

    [Fact]
    public void ResolveForInteractiveSwitch_ShouldLoad_WhenNoPreloadedBitmap()
    {
        var loaded = new object();
        var loaderCalled = false;

        var resolved = CrossPageSwitchBitmapResolver.ResolveForInteractiveSwitch(
            interactiveSwitch: true,
            preloadedBitmap: null,
            loadBitmap: () =>
            {
                loaderCalled = true;
                return loaded;
            });

        resolved.Should().BeSameAs(loaded);
        loaderCalled.Should().BeTrue();
    }

    [Fact]
    public void ResolveForInteractiveSwitch_ShouldLoad_WhenNotInteractiveSwitch()
    {
        var preloaded = new object();
        var loaded = new object();
        var loaderCalled = false;

        var resolved = CrossPageSwitchBitmapResolver.ResolveForInteractiveSwitch(
            interactiveSwitch: false,
            preloadedBitmap: preloaded,
            loadBitmap: () =>
            {
                loaderCalled = true;
                return loaded;
            });

        resolved.Should().BeSameAs(loaded);
        loaderCalled.Should().BeTrue();
    }
}
