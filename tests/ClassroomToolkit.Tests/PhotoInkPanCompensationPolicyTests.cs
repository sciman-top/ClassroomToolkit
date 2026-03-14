using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkPanCompensationPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnZero_WhenPhotoInkModeInactive()
    {
        var delta = PhotoInkPanCompensationPolicy.Resolve(
            photoInkModeActive: false,
            currentTranslateX: 120,
            currentTranslateY: -40,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: -10);

        delta.Should().Be(new Vector(0, 0));
    }

    [Fact]
    public void Resolve_ShouldReturnTranslateDelta_WhenPhotoInkModeActive()
    {
        var delta = PhotoInkPanCompensationPolicy.Resolve(
            photoInkModeActive: true,
            currentTranslateX: 120,
            currentTranslateY: -40,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: -10);

        delta.Should().Be(new Vector(20, -30));
    }
}
