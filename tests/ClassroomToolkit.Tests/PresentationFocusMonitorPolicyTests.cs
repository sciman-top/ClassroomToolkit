using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFocusMonitorPolicyTests
{
    [Fact]
    public void ShouldAttemptRestore_ShouldReturnTrue_WhenEnabled_NotCoolingDown_AndForegroundOwned()
    {
        var nowUtc = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc);

        var result = PresentationFocusMonitorPolicy.ShouldAttemptRestore(
            restoreEnabled: true,
            photoModeActive: false,
            boardActive: false,
            foregroundOwnedByCurrentProcess: true,
            nowUtc: nowUtc,
            nextAttemptUtc: nowUtc.AddMilliseconds(-1));

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldAttemptRestore_ShouldReturnFalse_WhenRestoreDisabled()
    {
        var nowUtc = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc);

        var result = PresentationFocusMonitorPolicy.ShouldAttemptRestore(
            restoreEnabled: false,
            photoModeActive: false,
            boardActive: false,
            foregroundOwnedByCurrentProcess: true,
            nowUtc: nowUtc,
            nextAttemptUtc: PresentationRuntimeDefaults.UnsetTimestampUtc);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ShouldAttemptRestore_ShouldReturnFalse_WhenPhotoOrBoardActive(bool photoModeActive, bool boardActive)
    {
        var nowUtc = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc);

        var result = PresentationFocusMonitorPolicy.ShouldAttemptRestore(
            restoreEnabled: true,
            photoModeActive: photoModeActive,
            boardActive: boardActive,
            foregroundOwnedByCurrentProcess: true,
            nowUtc: nowUtc,
            nextAttemptUtc: PresentationRuntimeDefaults.UnsetTimestampUtc);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttemptRestore_ShouldReturnFalse_WhenCoolingDown()
    {
        var nowUtc = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc);

        var result = PresentationFocusMonitorPolicy.ShouldAttemptRestore(
            restoreEnabled: true,
            photoModeActive: false,
            boardActive: false,
            foregroundOwnedByCurrentProcess: true,
            nowUtc: nowUtc,
            nextAttemptUtc: nowUtc.AddMilliseconds(1));

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttemptRestore_ShouldReturnFalse_WhenForegroundNotOwned()
    {
        var nowUtc = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc);

        var result = PresentationFocusMonitorPolicy.ShouldAttemptRestore(
            restoreEnabled: true,
            photoModeActive: false,
            boardActive: false,
            foregroundOwnedByCurrentProcess: false,
            nowUtc: nowUtc,
            nextAttemptUtc: PresentationRuntimeDefaults.UnsetTimestampUtc);

        result.Should().BeFalse();
    }

    [Fact]
    public void ComputeNextAttemptUtc_ShouldAddCooldown()
    {
        var nowUtc = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc);

        var result = PresentationFocusMonitorPolicy.ComputeNextAttemptUtc(nowUtc, 1200);

        result.Should().Be(nowUtc.AddMilliseconds(1200));
    }
}
