using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkOpacityMaskCacheTests
{
    [Fact]
    public void GetOrCreate_ShouldReuseEntryForEquivalentNormalizedInputs()
    {
        var cache = new InkOpacityMaskCache(capacity: 4);
        var factoryCalls = 0;

        var first = cache.GetOrCreate(
            new Rect(10, 20, 120, 80),
            inkFlow: 0.7,
            strokeDirection: new Vector(2, 0),
            brushSize: 12,
            seed: 17,
            textureVariant: InkOpacityMaskCache.PaintTextureVariant,
            factory: () =>
            {
                factoryCalls++;
                return new DrawingBrush();
            });
        var second = cache.GetOrCreate(
            new Rect(10, 20, 120, 80),
            inkFlow: 0.7,
            strokeDirection: new Vector(1, 0),
            brushSize: 12,
            seed: 17,
            textureVariant: InkOpacityMaskCache.PaintTextureVariant,
            factory: () =>
            {
                factoryCalls++;
                return new DrawingBrush();
            });

        factoryCalls.Should().Be(1);
        ReferenceEquals(first, second).Should().BeTrue();
        second!.IsFrozen.Should().BeTrue();
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void GetOrCreate_ShouldSeparateTextureVariants()
    {
        var cache = new InkOpacityMaskCache(capacity: 4);
        var factoryCalls = 0;

        cache.GetOrCreate(
            new Rect(0, 0, 20, 20),
            0.5,
            new Vector(1, 0),
            10,
            1,
            InkOpacityMaskCache.PaintTextureVariant,
            () =>
            {
                factoryCalls++;
                return new DrawingBrush();
            });
        cache.GetOrCreate(
            new Rect(0, 0, 20, 20),
            0.5,
            new Vector(1, 0),
            10,
            1,
            InkOpacityMaskCache.ExportTextureVariant,
            () =>
            {
                factoryCalls++;
                return new DrawingBrush();
            });

        factoryCalls.Should().Be(2);
        cache.Count.Should().Be(2);
    }

    [Fact]
    public void GetOrCreate_ShouldBoundRetainedEntries()
    {
        var cache = new InkOpacityMaskCache(capacity: 2);
        for (var i = 0; i < 3; i++)
        {
            cache.GetOrCreate(
                new Rect(i, 0, 20, 20),
                0.5,
                new Vector(1, 0),
                10,
                i,
                InkOpacityMaskCache.PaintTextureVariant,
                static () => new DrawingBrush());
        }

        cache.Count.Should().Be(1);
    }
}
