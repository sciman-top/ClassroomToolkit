using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsPresentationRuntimePolicyTests
{
    [Theory]
    [InlineData("wpp.exe")]
    [InlineData("wppt")]
    [InlineData("WpsPresentationHost.exe")]
    public void IsDedicatedSlideshowRuntime_ShouldRecognizeDedicatedRuntimeProcesses(string processName)
    {
        var dedicated = WpsPresentationRuntimePolicy.IsDedicatedSlideshowRuntime(processName);

        dedicated.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("wps.exe")]
    [InlineData("powerpnt.exe")]
    [InlineData("explorer.exe")]
    public void IsDedicatedSlideshowRuntime_ShouldRejectEditorAndUnrelatedProcesses(string processName)
    {
        var dedicated = WpsPresentationRuntimePolicy.IsDedicatedSlideshowRuntime(processName);

        dedicated.Should().BeFalse();
    }
}
