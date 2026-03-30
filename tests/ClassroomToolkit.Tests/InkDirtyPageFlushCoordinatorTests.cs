using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using System.Collections.Generic;

namespace ClassroomToolkit.Tests;

public sealed class InkDirtyPageFlushCoordinatorTests
{
    [Fact]
    public void Flush_ShouldDoNothing_WhenSaveDisabled()
    {
        var calls = 0;

        var result = InkDirtyPageFlushCoordinator.Flush(
            inkSaveEnabled: false,
            directoryPath: null,
            stopAutoSaveTimer: () => calls++,
            cancelAutoSaveGeneration: () => calls++,
            finalizeActiveOperation: () => calls++,
            getDirtyPages: _ => new List<(string SourcePath, int PageIndex)> { ("a", 1) },
            tryGetPageStrokes: (string _, int _, out List<InkStrokeData> strokes) =>
            {
                strokes = new List<InkStrokeData>();
                calls++;
                return true;
            },
            persistPage: (string _, int _, List<InkStrokeData> _, out string? error) =>
            {
                error = null;
                calls++;
                return true;
            });

        calls.Should().Be(0);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Flush_ShouldPersistOnlyPagesWithAvailableStrokes()
    {
        var persisted = new List<(string SourcePath, int PageIndex, int Count)>();
        var dirtyPages = new List<(string SourcePath, int PageIndex)>
        {
            ("a.pdf", 1),
            ("b.pdf", 2)
        };

        var result = InkDirtyPageFlushCoordinator.Flush(
            inkSaveEnabled: true,
            directoryPath: null,
            stopAutoSaveTimer: () => { },
            cancelAutoSaveGeneration: () => { },
            finalizeActiveOperation: () => { },
            getDirtyPages: _ => dirtyPages,
            tryGetPageStrokes: (string sourcePath, int pageIndex, out List<InkStrokeData> strokes) =>
            {
                if (sourcePath == "a.pdf" && pageIndex == 1)
                {
                    strokes = new List<InkStrokeData> { new() };
                    return true;
                }

                strokes = new List<InkStrokeData>();
                return false;
            },
            persistPage: (string sourcePath, int pageIndex, List<InkStrokeData> strokes, out string? error) =>
            {
                error = null;
                persisted.Add((sourcePath, pageIndex, strokes.Count));
                return true;
            });

        persisted.Should().ContainSingle();
        persisted[0].Should().Be(("a.pdf", 1, 1));
        result.IsSuccess.Should().BeTrue();
        result.AttemptedCount.Should().Be(1);
        result.SucceededCount.Should().Be(1);
    }
}
