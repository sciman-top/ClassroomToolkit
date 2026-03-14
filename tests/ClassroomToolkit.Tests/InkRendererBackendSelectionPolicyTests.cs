using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkRendererBackendSelectionPolicyTests
{
    [Fact]
    public void Resolve_ShouldUseCpu_WhenGpuNotPreferred()
    {
        var kind = InkRendererBackendSelectionPolicy.Resolve(preferGpu: false, gpuAvailable: true);

        kind.Should().Be(InkRendererBackendKind.Cpu);
    }

    [Fact]
    public void Resolve_ShouldUseCpu_WhenGpuUnavailable()
    {
        var kind = InkRendererBackendSelectionPolicy.Resolve(preferGpu: true, gpuAvailable: false);

        kind.Should().Be(InkRendererBackendKind.Cpu);
    }

    [Fact]
    public void Resolve_ShouldUseGpu_WhenPreferredAndAvailable()
    {
        var kind = InkRendererBackendSelectionPolicy.Resolve(preferGpu: true, gpuAvailable: true);

        kind.Should().Be(InkRendererBackendKind.Gpu);
    }
}
