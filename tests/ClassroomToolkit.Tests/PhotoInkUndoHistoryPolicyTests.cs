using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

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
        InkPersistencePolicies.ShouldTrackVectorSnapshot(inkRecordEnabled, photoInkModeActive)
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
        InkPersistencePolicies.ShouldPreferGlobalPhotoUndo(photoModeActive, globalHistoryCount)
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
        InkPersistencePolicies.ShouldPreferLocalVectorUndo(
                inkRecordEnabled,
                photoInkModeActive,
                localHistoryCount)
            .Should()
            .Be(expected);
    }
}
