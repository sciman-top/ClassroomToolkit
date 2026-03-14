using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkRendererFactoryResolverTests
{
    [Fact]
    public void Resolve_ShouldFallbackToCpu_WhenGpuPreferred()
    {
        var factory = InkRendererFactoryResolver.Resolve(preferGpu: true, out var requested);

        requested.Should().Be("gpu");
        factory.Should().BeOfType<CpuInkRendererFactory>();
        factory.BackendId.Should().Be("cpu");
    }

    [Fact]
    public void Resolve_ShouldReturnGpuFactory_WhenGpuProbeAvailable()
    {
        var factory = InkRendererFactoryResolver.Resolve(
            preferGpu: true,
            gpuAvailabilityProbe: () => true,
            out var requested);

        requested.Should().Be("gpu");
        factory.Should().BeOfType<GpuInkRendererFactory>();
        factory.BackendId.Should().Be("gpu");
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenProbeIsNull()
    {
        Action act = () => InkRendererFactoryResolver.Resolve(
            preferGpu: true,
            gpuAvailabilityProbe: null!,
            out _);

        act.Should().Throw<ArgumentNullException>();
    }
}
