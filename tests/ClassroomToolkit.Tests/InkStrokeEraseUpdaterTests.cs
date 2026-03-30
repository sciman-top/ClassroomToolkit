using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using System.Windows;
using System.Windows.Media;

namespace ClassroomToolkit.Tests;

public sealed class InkStrokeEraseUpdaterTests
{
    [Fact]
    public void TryApplyUpdatedGeometryPath_ShouldReturnFalse_WhenUpdatedPathIsNull()
    {
        var stroke = CreateStroke();

        var changed = InkStrokeEraseUpdater.TryApplyUpdatedGeometryPath(stroke, null, out var removed);

        changed.Should().BeFalse();
        removed.Should().BeFalse();
        stroke.CachedGeometry.Should().NotBeNull();
        stroke.CachedBounds.Should().NotBeNull();
    }

    [Fact]
    public void TryApplyUpdatedGeometryPath_ShouldMarkRemoved_WhenUpdatedPathIsEmpty()
    {
        var stroke = CreateStroke();

        var changed = InkStrokeEraseUpdater.TryApplyUpdatedGeometryPath(stroke, string.Empty, out var removed);

        changed.Should().BeTrue();
        removed.Should().BeTrue();
    }

    [Fact]
    public void TryApplyUpdatedGeometryPath_ShouldInvalidateCachedGeometry_WhenPathChanges()
    {
        var stroke = CreateStroke();
        var newPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(12, 12, 8, 8)));

        var changed = InkStrokeEraseUpdater.TryApplyUpdatedGeometryPath(stroke, newPath, out var removed);

        changed.Should().BeTrue();
        removed.Should().BeFalse();
        stroke.GeometryPath.Should().Be(newPath);
        stroke.CachedGeometry.Should().BeNull();
        stroke.CachedBounds.Should().BeNull();
    }

    private static InkStrokeData CreateStroke()
    {
        var geometry = new RectangleGeometry(new Rect(10, 10, 20, 20));
        return new InkStrokeData
        {
            GeometryPath = InkGeometrySerializer.Serialize(geometry),
            CachedGeometry = geometry,
            CachedBounds = geometry.Bounds
        };
    }
}
