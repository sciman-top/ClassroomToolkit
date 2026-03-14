using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkPersistenceTogglePolicyTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldLoadPersistedInk_ShouldFollowDiskFallbackGate(bool allowDiskFallback, bool expected)
    {
        InkPersistenceTogglePolicy.ShouldLoadPersistedInk(allowDiskFallback)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldTrackWal_ShouldFollowSaveToggle(bool inkSaveEnabled, bool expected)
    {
        InkPersistenceTogglePolicy.ShouldTrackWal(inkSaveEnabled)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldRecoverWal_ShouldFollowSaveToggle(bool inkSaveEnabled, bool expected)
    {
        InkPersistenceTogglePolicy.ShouldRecoverWal(inkSaveEnabled)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldRetainRuntimeCacheOnPhotoExit_ShouldFollowSaveToggle(bool inkSaveEnabled, bool expected)
    {
        InkPersistenceTogglePolicy.ShouldRetainRuntimeCacheOnPhotoExit(inkSaveEnabled)
            .Should().Be(expected);
    }
}
