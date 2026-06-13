using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkUndoHistoryPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ShouldTrackVectorSnapshot_ShouldKeepPhotoUndoIndependentFromRecordSetting(
        bool inkRecordEnabled,
        bool photoInkModeActive,
        bool expected)
    {
        InkUndoHistoryPolicy.ShouldTrackVectorSnapshot(inkRecordEnabled, photoInkModeActive)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(false, 1, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    public void ShouldPreferGlobalPhotoUndo_ShouldUsePhotoModeAndAvailableSnapshots(
        bool photoModeActive,
        int globalHistoryCount,
        bool expected)
    {
        InkUndoHistoryPolicy.ShouldPreferGlobalPhotoUndo(photoModeActive, globalHistoryCount)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(false, false, 1, false)]
    [InlineData(true, false, 1, true)]
    [InlineData(false, true, 1, true)]
    [InlineData(false, true, 0, false)]
    public void ShouldPreferLocalVectorUndo_ShouldUsePhotoRuntimeHistoryEvenWhenRecordDisabled(
        bool inkRecordEnabled,
        bool photoInkModeActive,
        int localHistoryCount,
        bool expected)
    {
        InkUndoHistoryPolicy.ShouldPreferLocalVectorUndo(
                inkRecordEnabled,
                photoInkModeActive,
                localHistoryCount)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void PaintOverlayUndo_ShouldRouteHistoryThroughPhotoRuntimePolicy()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain(
            "InkUndoHistoryPolicy.ShouldTrackVectorSnapshot(_inkRecordEnabled, IsPhotoInkModeActive())");
        source.Should().Contain(
            "InkUndoHistoryPolicy.ShouldPreferGlobalPhotoUndo(_photoModeActive, _globalInkHistory.Count)");
        source.Should().Contain(
            "InkUndoHistoryPolicy.ShouldPreferLocalVectorUndo(_inkRecordEnabled, IsPhotoInkModeActive(), _inkHistory.Count)");
    }
}
