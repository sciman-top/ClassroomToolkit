using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkCacheRuntimeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InkCacheRuntimeDefaults.HistoryLimit.Should().Be(20);
        InkCacheRuntimeDefaults.MaxHistoryMemoryBytes.Should().Be(512L * 1024L * 1024L);
        InkCacheRuntimeDefaults.NoiseTileCacheLimit.Should().Be(96);
        InkCacheRuntimeDefaults.SolidBrushCacheLimit.Should().Be(256);
        InkCacheRuntimeDefaults.PenCacheLimit.Should().Be(192);
    }
}
