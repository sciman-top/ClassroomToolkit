using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFocusMonitorActivationPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, true, true)]
    public void ShouldMonitor_ShouldMatchExpected(
        bool overlayVisible,
        bool allowOffice,
        bool allowWps,
        bool photoFullscreenActive,
        bool expected)
    {
        PresentationPipelinePolicies.ShouldMonitor(
                overlayVisible,
                allowOffice,
                allowWps,
                photoFullscreenActive)
            .Should()
            .Be(expected);
    }
}

public sealed class PresentationFocusMonitorPolicyTests
{
    [Fact]
    public void ShouldAttemptRestore_ShouldReturnTrue_WhenEnabled_NotCoolingDown_AndForegroundOwned()
    {
        var nowUtc = new DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc);

        var result = PresentationPipelinePolicies.ShouldAttemptRestore(
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

        var result = PresentationPipelinePolicies.ShouldAttemptRestore(
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

        var result = PresentationPipelinePolicies.ShouldAttemptRestore(
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

        var result = PresentationPipelinePolicies.ShouldAttemptRestore(
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

        var result = PresentationPipelinePolicies.ShouldAttemptRestore(
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

        var result = PresentationPipelinePolicies.ComputeNextAttemptUtc(nowUtc, 1200);

        result.Should().Be(nowUtc.AddMilliseconds(1200));
    }
}

[Collection(SharedWindowDragStateCollection.Name)]
public sealed class PresentationFocusRestorePolicyTests
{
    [Fact]
    public void CanRestore_ShouldReturnTrue_WhenCursorHybridAndForegroundAllowed()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        var result = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRestore_ShouldReturnFalse_WhenToolModeIsDraw()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new EnterPresentationFullscreenEvent(PresentationSourceKind.Wps));

        var result = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldReturnFalse_WhenPhotoOrBoardActive()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        var photo = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: true,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);
        var board = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: false,
            boardActive: true,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldRespectFullscreenRequirement()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.Wps));

        var result = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: false,
            requireFullscreen: true,
            forceForeground: true,
            foregroundOwnedByCurrentProcess: false,
            dragOperationActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldAllow_WhenHookOnlyAndForcedForeground()
    {
        var state = UiSessionState.Default with
        {
            ToolMode = UiToolMode.Cursor,
            NavigationMode = UiNavigationMode.HookOnly,
            Scene = UiSceneKind.PresentationFullscreen,
            FocusOwner = UiFocusOwner.Presentation
        };

        var result = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: true,
            foregroundOwnedByCurrentProcess: false,
            dragOperationActive: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRestore_ShouldReturnFalse_WhenWindowDragOperationIsActive()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        var result = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldFollowWindowDragOperationState()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        using var dragScope = WindowDragOperationState.Begin();
        var result = PresentationPipelinePolicies.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: WindowDragOperationState.IsActive);

        result.Should().BeFalse();
    }
}

public sealed class PresentationFollowMonitorPolicyTests
{
    [Fact]
    public void ShouldFollow_ShouldReturnTrue_WhenPresentationSceneIsActive()
    {
        PresentationPipelinePolicies.ShouldFollow(
            photoModeActive: false,
            boardActive: false,
            overlayVisible: true,
            windowStateMinimized: false).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, true)]
    public void ShouldFollow_ShouldReturnFalse_WhenSceneOwnsGeometryOrOverlayUnavailable(
        bool photoModeActive,
        bool boardActive,
        bool overlayVisible,
        bool windowStateMinimized)
    {
        PresentationPipelinePolicies.ShouldFollow(
            photoModeActive,
            boardActive,
            overlayVisible,
            windowStateMinimized).Should().BeFalse();
    }

    [Fact]
    public void ShouldMove_ShouldReturnFalse_WhenOverlayAlreadyOnTargetMonitor()
    {
        var rect = new Rect(1920, 0, 1920, 1080);

        PresentationPipelinePolicies.ShouldMove(rect, rect).Should().BeFalse();
    }

    [Fact]
    public void ShouldMove_ShouldReturnTrue_WhenTargetMonitorDiffers()
    {
        PresentationPipelinePolicies.ShouldMove(
            new Rect(0, 0, 1920, 1080),
            new Rect(1920, 0, 1920, 1080)).Should().BeTrue();
    }
}

public sealed class PresentationFullscreenTypeResolutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnWps_WhenOnlyWpsFullscreen()
    {
        var resolved = PresentationPipelinePolicies.Resolve(
            wpsFullscreen: true,
            officeFullscreen: false,
            currentPresentationType: PresentationType.None);

        resolved.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void Resolve_ShouldReturnOffice_WhenOnlyOfficeFullscreen()
    {
        var resolved = PresentationPipelinePolicies.Resolve(
            wpsFullscreen: false,
            officeFullscreen: true,
            currentPresentationType: PresentationType.None);

        resolved.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentType_WhenBothFullscreenAndCurrentKnown()
    {
        var resolved = PresentationPipelinePolicies.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Wps,
            foregroundType: PresentationType.Other,
            foregroundIsFullscreen: true);

        resolved.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void Resolve_ShouldPreferWps_WhenBothFullscreenAndForegroundIsWps()
    {
        var resolved = PresentationPipelinePolicies.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Office,
            foregroundType: PresentationType.Wps,
            foregroundIsFullscreen: true);

        resolved.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void Resolve_ShouldPreferOffice_WhenBothFullscreenAndForegroundIsOffice()
    {
        var resolved = PresentationPipelinePolicies.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Wps,
            foregroundType: PresentationType.Office,
            foregroundIsFullscreen: true);

        resolved.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenAmbiguousAndCurrentUnknown()
    {
        var resolved = PresentationPipelinePolicies.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.None,
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false);

        resolved.Should().Be(PresentationType.None);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenCurrentTypeIsNotAChannel()
    {
        var resolved = PresentationPipelinePolicies.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Other);

        resolved.Should().Be(PresentationType.None);
    }
}

public sealed class PresentationFullscreenWindowAdmissionPolicyTests
{
    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldAllowOfficeFullscreenFallback_WhenClassNotSlideshow()
    {
        var allowed = PresentationPipelinePolicies.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: false);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldAllowDedicatedWpsRuntimeWithoutSlideshowClass()
    {
        var allowed = PresentationPipelinePolicies.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: false,
            classifiesAsDedicatedWpsRuntime: true);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldRejectWpsEditorFullscreenWithoutSlideshowClass()
    {
        var allowed = PresentationPipelinePolicies.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: false,
            classifiesAsDedicatedWpsRuntime: false);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldRejectNonFullscreenEvenWhenOffice()
    {
        var allowed = PresentationPipelinePolicies.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: false,
            classifiesAsSlideshow: false,
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: true);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldRejectInvalidOrMissingTargetInfo()
    {
        var invalid = PresentationPipelinePolicies.ShouldTreatAsPresentationFullscreen(
            targetIsValid: false,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: true,
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: true);
        var missingInfo = PresentationPipelinePolicies.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: false,
            isFullscreen: true,
            classifiesAsSlideshow: true,
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: true);

        invalid.Should().BeFalse();
        missingInfo.Should().BeFalse();
    }
}

public sealed class PresentationInputFocusPolicyTests
{
    [Fact]
    public void IsAuthorizedForeground_ShouldRequireExactOverlayOrToolbarHandle()
    {
        PresentationPipelinePolicies.IsAuthorizedForeground(
            new IntPtr(10),
            new IntPtr(10),
            new IntPtr(20)).Should().BeTrue();

        PresentationPipelinePolicies.IsAuthorizedForeground(
            new IntPtr(20),
            new IntPtr(10),
            new IntPtr(20)).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorizedForeground_ShouldRejectOtherSameProcessWindow()
    {
        PresentationPipelinePolicies.IsAuthorizedForeground(
            new IntPtr(30),
            new IntPtr(10),
            new IntPtr(20)).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedForeground_ShouldRejectZeroHandle()
    {
        PresentationPipelinePolicies.IsAuthorizedForeground(
            IntPtr.Zero,
            new IntPtr(10),
            new IntPtr(20)).Should().BeFalse();
    }
}

public sealed class PresentationKeyCommandPolicyTests
{
    [Theory]
    [InlineData(Key.Right, PresentationCommand.Next)]
    [InlineData(Key.Down, PresentationCommand.Next)]
    [InlineData(Key.Space, PresentationCommand.Next)]
    [InlineData(Key.Enter, PresentationCommand.Next)]
    [InlineData(Key.PageDown, PresentationCommand.Next)]
    [InlineData(Key.Left, PresentationCommand.Previous)]
    [InlineData(Key.Up, PresentationCommand.Previous)]
    [InlineData(Key.PageUp, PresentationCommand.Previous)]
    [InlineData(Key.Home, PresentationCommand.First)]
    [InlineData(Key.End, PresentationCommand.Last)]
    public void TryMap_ShouldReturnExpectedCommand(Key key, PresentationCommand expected)
    {
        var ok = PresentationPipelinePolicies.TryMap(key, out var command);

        ok.Should().BeTrue();
        command.Should().Be(expected);
    }

    [Fact]
    public void TryMap_ShouldReturnFalse_ForUnsupportedKey()
    {
        var ok = PresentationPipelinePolicies.TryMap(Key.A, out _);

        ok.Should().BeFalse();
    }
}

public sealed class PresentationNavigationAdmissionPolicyTests
{
    [Fact]
    public void ShouldAttempt_ShouldReturnFalse_WhenChannelNotAllowed()
    {
        var allowed = PresentationPipelinePolicies.ShouldAttempt(
            allowChannel: false,
            boardActive: false,
            targetIsValid: true,
            targetHasInfo: true,
            targetIsSlideshow: true,
            allowBackground: true,
            targetForeground: false);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttempt_ShouldReturnFalse_WhenBoardActive()
    {
        var allowed = PresentationPipelinePolicies.ShouldAttempt(
            allowChannel: true,
            boardActive: true,
            targetIsValid: true,
            targetHasInfo: true,
            targetIsSlideshow: true,
            allowBackground: true,
            targetForeground: false);

        allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void ShouldAttempt_ShouldRequireValidTargetAndForegroundWhenNeeded(
        bool targetIsValid,
        bool targetHasInfo,
        bool targetIsSlideshow,
        bool targetForeground,
        bool expected)
    {
        var allowed = PresentationPipelinePolicies.ShouldAttempt(
            allowChannel: true,
            boardActive: false,
            targetIsValid: targetIsValid,
            targetHasInfo: targetHasInfo,
            targetIsSlideshow: targetIsSlideshow,
            allowBackground: false,
            targetForeground: targetForeground);

        allowed.Should().Be(expected);
    }

    [Fact]
    public void ShouldAttempt_ShouldIgnoreForeground_WhenBackgroundAllowed()
    {
        var allowed = PresentationPipelinePolicies.ShouldAttempt(
            allowChannel: true,
            boardActive: false,
            targetIsValid: true,
            targetHasInfo: true,
            targetIsSlideshow: true,
            allowBackground: true,
            targetForeground: false);

        allowed.Should().BeTrue();
    }
}

public sealed class PresentationOverlayRetouchPolicyTests
{
    [Fact]
    public void ShouldRequest_ShouldReturnTrue_WhenPresentationActionCanCoverVisibleFullscreenOverlay()
    {
        var result = PresentationPipelinePolicies.ShouldRequest(
            presentationActionApplied: true,
            overlayVisible: true,
            presentationFullscreenActive: true);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldRequest_ShouldReturnFalse_WhenRetouchWouldBeNoise(
        bool presentationActionApplied,
        bool overlayVisible,
        bool presentationFullscreenActive)
    {
        var result = PresentationPipelinePolicies.ShouldRequest(
            presentationActionApplied,
            overlayVisible,
            presentationFullscreenActive);

        result.Should().BeFalse();
    }
}

public sealed class PresentationReservedNavigationKeyPolicyTests
{
    [Fact]
    public void ResolveRollCallGroupSwitchKeys_ShouldReserveEnter_WhenGroupSwitchUsesEnter()
    {
        var keys = PresentationPipelinePolicies.ResolveRollCallGroupSwitchKeys(
            enabled: true,
            configuredKey: "enter");

        keys.Should().ContainSingle().Which.Should().Be(VirtualKey.Enter);
    }

    [Fact]
    public void ResolveRollCallGroupSwitchKeys_ShouldReturnEmpty_WhenGroupSwitchDisabled()
    {
        var keys = PresentationPipelinePolicies.ResolveRollCallGroupSwitchKeys(
            enabled: false,
            configuredKey: "enter");

        keys.Should().BeEmpty();
    }

    [Fact]
    public void ResolveRollCallGroupSwitchKeys_ShouldFallbackToEnter_WhenConfiguredKeyBlank()
    {
        var keys = PresentationPipelinePolicies.ResolveRollCallGroupSwitchKeys(
            enabled: true,
            configuredKey: " ");

        keys.Should().ContainSingle().Which.Should().Be(VirtualKey.Enter);
    }
}

public sealed class PresentationSlideshowDetectionPolicyTests
{
    [Fact]
    public void IsSlideshow_ShouldReturnFalse_WhenTargetInvalid()
    {
        var classifier = new PresentationClassifier();

        var result = PresentationPipelinePolicies.IsSlideshow(
            PresentationTarget.Empty,
            classifier,
            _ => true);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsSlideshow_ShouldReturnTrue_WhenClassifierMatches()
    {
        var classifier = new PresentationClassifier();
        var target = new PresentationTarget(
            new IntPtr(1001),
            new PresentationWindowInfo(1, "powerpnt.exe", new[] { "screenclass" }));

        var result = PresentationPipelinePolicies.IsSlideshow(
            target,
            classifier,
            _ => false);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSlideshow_ShouldFallbackToFullscreen_WhenClassifierMisses()
    {
        var classifier = new PresentationClassifier();
        var target = new PresentationTarget(
            new IntPtr(2002),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "randomclass" }));

        var result = PresentationPipelinePolicies.IsSlideshow(
            target,
            classifier,
            _ => true);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSlideshow_ShouldRejectGenericWpsEditorFullscreen()
    {
        var classifier = new PresentationClassifier(new PresentationClassifierOverrides(
            AdditionalWpsClassTokens: [],
            AdditionalOfficeClassTokens: [],
            AdditionalSlideshowClassTokens: [],
            AdditionalWpsProcessTokens: ["wps-editor"],
            AdditionalOfficeProcessTokens: []));
        var target = new PresentationTarget(
            new IntPtr(3003),
            new PresentationWindowInfo(1, "wps-editor.exe", new[] { "randomclass" }));

        var result = PresentationPipelinePolicies.IsSlideshow(
            target,
            classifier,
            _ => true);

        result.Should().BeFalse();
    }
}

public sealed class PresentationTargetChannelSelectionPolicyTests
{
    [Fact]
    public void ResolveForFocus_ShouldPreferFullscreenForegroundChannel()
    {
        var type = PresentationPipelinePolicies.ResolveForFocus(
            foregroundType: PresentationType.Office,
            foregroundIsFullscreen: true,
            currentPresentationType: PresentationType.Wps,
            allowWps: true,
            allowOffice: true);

        type.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void ResolveForFocus_ShouldKeepCurrentChannel_WhenForegroundHasNoPresentationEvidence()
    {
        var type = PresentationPipelinePolicies.ResolveForFocus(
            foregroundType: PresentationType.Other,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.Wps,
            allowWps: true,
            allowOffice: true);

        type.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void ResolveForFocus_ShouldUseTheOnlyEnabledChannel()
    {
        var type = PresentationPipelinePolicies.ResolveForFocus(
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.None,
            allowWps: false,
            allowOffice: true);

        type.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void ResolveForFocus_ShouldFailClosed_WhenBothChannelsAreEnabledButAmbiguous()
    {
        var type = PresentationPipelinePolicies.ResolveForFocus(
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.None,
            allowWps: true,
            allowOffice: true);

        type.Should().Be(PresentationType.None);
    }

    [Fact]
    public void ResolveForFocus_ShouldIgnoreCurrentChannel_WhenItIsDisabled()
    {
        var type = PresentationPipelinePolicies.ResolveForFocus(
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.Wps,
            allowWps: false,
            allowOffice: true);

        type.Should().Be(PresentationType.Office);
    }
}

public sealed class PresentationWheelInkConflictPolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_InCursorMode()
    {
        var suppressed = PresentationPipelinePolicies.ShouldSuppress(
            PaintToolMode.Cursor,
            DateTime.UtcNow,
            DateTime.UtcNow,
            suppressWindowMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenNoRecentInkInput()
    {
        var suppressed = PresentationPipelinePolicies.ShouldSuppress(
            PaintToolMode.Brush,
            InkRuntimeTimingDefaults.UnsetTimestampUtc,
            DateTime.UtcNow,
            suppressWindowMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenWithinSuppressWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var suppressed = PresentationPipelinePolicies.ShouldSuppress(
            PaintToolMode.Brush,
            nowUtc.AddMilliseconds(-60),
            nowUtc,
            suppressWindowMs: 120);

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenOutsideSuppressWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var suppressed = PresentationPipelinePolicies.ShouldSuppress(
            PaintToolMode.Brush,
            nowUtc.AddMilliseconds(-220),
            nowUtc,
            suppressWindowMs: 120);

        suppressed.Should().BeFalse();
    }
}

