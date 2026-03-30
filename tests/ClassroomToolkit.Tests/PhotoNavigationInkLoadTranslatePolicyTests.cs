using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoNavigationInkLoadTranslatePolicyTests
{
    [Fact]
    public void ResolveTranslateYBeforeLoad_ShouldUseTargetTranslate_WhenCrossPageInkPageChanged()
    {
        var translateY = PhotoNavigationInkLoadTranslatePolicy.ResolveTranslateYBeforeLoad(
            currentTranslateY: 120,
            targetTranslateY: -860,
            pageChanged: true,
            photoInkModeActive: true,
            crossPageDisplayActive: true);

        translateY.Should().Be(-860);
    }

    [Fact]
    public void ResolveTranslateYBeforeLoad_ShouldKeepCurrentTranslate_WhenNotCrossPageDisplay()
    {
        var translateY = PhotoNavigationInkLoadTranslatePolicy.ResolveTranslateYBeforeLoad(
            currentTranslateY: 120,
            targetTranslateY: -860,
            pageChanged: true,
            photoInkModeActive: true,
            crossPageDisplayActive: false);

        translateY.Should().Be(120);
    }

    [Fact]
    public void ResolveTranslateYBeforeLoad_ShouldKeepCurrentTranslate_WhenPageNotChanged()
    {
        var translateY = PhotoNavigationInkLoadTranslatePolicy.ResolveTranslateYBeforeLoad(
            currentTranslateY: 120,
            targetTranslateY: -860,
            pageChanged: false,
            photoInkModeActive: true,
            crossPageDisplayActive: true);

        translateY.Should().Be(120);
    }
}
