using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerInkCleanupExecutorTests
{
    [Fact]
    public void Cleanup_ShouldInvokeBothCleanupActions_ForValidFolder()
    {
        var sidecarCalls = 0;
        var compositeCalls = 0;

        var summary = ImageManagerInkCleanupExecutor.Cleanup(
            @"E:\tmp\photos",
            _ =>
            {
                sidecarCalls++;
                return 2;
            },
            _ =>
            {
                compositeCalls++;
                return 3;
            });

        sidecarCalls.Should().Be(1);
        compositeCalls.Should().Be(1);
        summary.SidecarsDeleted.Should().Be(2);
        summary.CompositesDeleted.Should().Be(3);
    }

    [Fact]
    public void Cleanup_ShouldSkipActions_WhenFolderIsEmpty()
    {
        var sidecarCalls = 0;
        var compositeCalls = 0;

        var summary = ImageManagerInkCleanupExecutor.Cleanup(
            string.Empty,
            _ =>
            {
                sidecarCalls++;
                return 1;
            },
            _ =>
            {
                compositeCalls++;
                return 1;
            });

        sidecarCalls.Should().Be(0);
        compositeCalls.Should().Be(0);
        summary.SidecarsDeleted.Should().Be(0);
        summary.CompositesDeleted.Should().Be(0);
    }
}
