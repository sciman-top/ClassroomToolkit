using ClassroomToolkit.App.Paint;
using FluentAssertions;
using System.IO;
using System.Linq;

namespace ClassroomToolkit.Tests;

public sealed class InkDirtyPageCoordinatorTests
{
    [Fact]
    public void MarkModified_ThenPersisted_ShouldToggleDirtyFlag()
    {
        var coordinator = new InkDirtyPageCoordinator();
        const string source = @"E:\x\a.pdf";

        coordinator.MarkModified(source, 2, "h1");
        coordinator.IsDirty(source, 2).Should().BeTrue();

        coordinator.MarkPersisted(source, 2, "h2");
        coordinator.IsDirty(source, 2).Should().BeFalse();
    }

    [Fact]
    public void MarkModified_ThenLoaded_ShouldClearDirtyFlag()
    {
        var coordinator = new InkDirtyPageCoordinator();
        const string source = @"E:\x\loaded.pdf";

        coordinator.MarkModified(source, 1, "h1");
        coordinator.IsDirty(source, 1).Should().BeTrue();

        coordinator.MarkLoaded(source, 1, "h2");
        coordinator.IsDirty(source, 1).Should().BeFalse();
    }

    [Fact]
    public void MarkPersistedIfUnchanged_ShouldKeepDirty_WhenHashAdvanced()
    {
        var coordinator = new InkDirtyPageCoordinator();
        const string source = @"E:\x\race.pdf";

        coordinator.MarkModified(source, 1, "h1");
        coordinator.MarkModified(source, 1, "h2");

        var marked = coordinator.MarkPersistedIfUnchanged(source, 1, "h1");

        marked.Should().BeFalse();
        coordinator.IsDirty(source, 1).Should().BeTrue();
    }

    [Fact]
    public void EnumerateSessionModifiedSourcesInDirectory_ShouldReturnOnlyMatchingDirectory()
    {
        var coordinator = new InkDirtyPageCoordinator();
        var dir = Path.Combine(Path.GetTempPath(), "ctk-coordinator");
        var sourceA = Path.Combine(dir, "a.pdf");
        var sourceB = Path.Combine(dir, "sub", "b.pdf");

        coordinator.MarkModified(sourceA, 1, "ha");
        coordinator.MarkModified(sourceB, 1, "hb");

        var listed = coordinator.EnumerateSessionModifiedSourcesInDirectory(dir).ToList();
        listed.Should().Contain(sourceA);
        listed.Should().NotContain(sourceB);
    }

    [Fact]
    public void GetDirtyPages_ShouldApplyDirectoryFilter()
    {
        var coordinator = new InkDirtyPageCoordinator();
        var dir = Path.Combine(Path.GetTempPath(), "ctk-coordinator-2");
        var file1 = Path.Combine(dir, "1.png");
        var file2 = Path.Combine(Path.GetTempPath(), "outside", "2.png");

        coordinator.MarkModified(file1, 1, "a");
        coordinator.MarkModified(file2, 3, "b");

        var dirtyInDir = coordinator.GetDirtyPages(dir);
        dirtyInDir.Should().ContainSingle()
            .Which.Should().Be((file1, 1));
    }
}
