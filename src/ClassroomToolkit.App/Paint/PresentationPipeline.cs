using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationFocusMonitorActivationPolicy
{
    internal static bool ShouldMonitor(
        bool overlayVisible,
        bool allowOffice,
        bool allowWps,
        bool photoFullscreenActive)
    {
        return overlayVisible && (allowOffice || allowWps || photoFullscreenActive);
    }
}

internal static class PresentationFocusMonitorPolicy
{
    internal static bool ShouldAttemptRestore(
        bool restoreEnabled,
        bool photoModeActive,
        bool boardActive,
        bool foregroundOwnedByCurrentProcess,
        DateTime nowUtc,
        DateTime nextAttemptUtc)
    {
        if (!restoreEnabled || photoModeActive || boardActive)
        {
            return false;
        }

        if (nowUtc < nextAttemptUtc)
        {
            return false;
        }

        return foregroundOwnedByCurrentProcess;
    }

    internal static DateTime ComputeNextAttemptUtc(DateTime nowUtc, int cooldownMs)
    {
        return nowUtc.AddMilliseconds(cooldownMs);
    }
}

internal static class PresentationFocusRestorePolicy
{
    internal static bool CanRestore(
        UiSessionState sessionState,
        bool photoModeActive,
        bool boardActive,
        bool isVisible,
        bool presentationAllowed,
        bool targetIsValid,
        bool targetIsSlideshow,
        bool targetIsFullscreen,
        bool requireFullscreen,
        bool forceForeground,
        bool foregroundOwnedByCurrentProcess,
        bool dragOperationActive)
    {
        if (!isVisible || photoModeActive || boardActive || dragOperationActive)
        {
            return false;
        }

        if (!presentationAllowed)
        {
            return false;
        }

        if (sessionState.ToolMode != UiToolMode.Cursor)
        {
            return false;
        }

        if (!UiSessionPresentationInputPolicy.AllowsPresentationInput(sessionState.NavigationMode))
        {
            return false;
        }

        if (!targetIsValid || !targetIsSlideshow)
        {
            return false;
        }

        if (requireFullscreen && !targetIsFullscreen)
        {
            return false;
        }

        if (!forceForeground && !foregroundOwnedByCurrentProcess)
        {
            return false;
        }

        return true;
    }
}

internal static class PresentationFollowMonitorPolicy
{
    /// <summary>
    /// 放映在副屏而覆盖层停留在主屏时，批注会画在放映画面之外。
    /// 进入放映全屏时覆盖层应搬到放映窗所在显示器；板书/照片模式有自己的
    /// 几何语义，不参与跟随。
    /// </summary>
    internal static bool ShouldFollow(
        bool photoModeActive,
        bool boardActive,
        bool overlayVisible,
        bool windowStateMinimized)
    {
        return !photoModeActive && !boardActive && overlayVisible && !windowStateMinimized;
    }

    internal static bool ShouldMove(Rect currentMonitorRect, Rect targetMonitorRect)
    {
        return !currentMonitorRect.Equals(targetMonitorRect);
    }
}

internal static class PresentationFullscreenTypeResolutionPolicy
{
    internal static PresentationType Resolve(
        bool wpsFullscreen,
        bool officeFullscreen,
        PresentationType currentPresentationType,
        PresentationType foregroundType = PresentationType.None,
        bool foregroundIsFullscreen = false)
    {
        // When both applications have a fullscreen candidate, the fresh
        // foreground window is the only safe discriminator.  A cached current
        // type may belong to the previous monitor or slideshow session.
        if (foregroundIsFullscreen
            && foregroundType == PresentationType.Wps
            && wpsFullscreen)
        {
            return PresentationType.Wps;
        }

        if (foregroundIsFullscreen
            && foregroundType == PresentationType.Office
            && officeFullscreen)
        {
            return PresentationType.Office;
        }

        if (wpsFullscreen && !officeFullscreen)
        {
            return PresentationType.Wps;
        }

        if (officeFullscreen && !wpsFullscreen)
        {
            return PresentationType.Office;
        }

        if (wpsFullscreen
            && officeFullscreen
            && currentPresentationType is PresentationType.Wps or PresentationType.Office)
        {
            return currentPresentationType;
        }

        return PresentationType.None;
    }
}

internal static class PresentationFullscreenWindowAdmissionPolicy
{
    internal static bool ShouldTreatAsPresentationFullscreen(
        bool targetIsValid,
        bool targetHasInfo,
        bool isFullscreen,
        bool classifiesAsSlideshow,
        bool classifiesAsOffice,
        bool classifiesAsDedicatedWpsRuntime)
    {
        if (!targetIsValid || !targetHasInfo || !isFullscreen)
        {
            return false;
        }

        if (classifiesAsSlideshow)
        {
            return true;
        }

        // Office slideshow may switch runtime classes in pen/annotation mode.
        if (classifiesAsOffice)
        {
            return true;
        }

        // Newer WPS builds host slideshow in a dedicated wpp/wppt runtime whose
        // top-level window may expose only generic Qt classes.
        return classifiesAsDedicatedWpsRuntime;
    }
}

internal static class PresentationInkExitSnapshotPolicy
{
    /// <summary>
    /// 放映批注没有逐页落盘（CacheScope=None），退出放映会清空表面；
    /// 有墨迹时必须先留一张 PNG 快照兜底，避免教师批注静默丢失。
    /// 只看位图表面是否有墨迹：出厂默认 ink_record_enabled=false 时
    /// 笔画不进入向量表（strokeCount 恒为 0），不能作为判据。
    /// </summary>
    internal static bool ShouldCapture(bool hasDrawing)
    {
        return hasDrawing;
    }
}

internal static class PresentationInkSnapshotNamer
{
    internal static string BuildFileName(DateTime localTime)
    {
        return $"presentation_{localTime.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture)}.png";
    }
}

/// <summary>
/// Defines the small set of application windows that may act as the source of
/// a background presentation-navigation request.  Process ownership alone is
/// intentionally insufficient: settings dialogs, roll-call windows and text
/// editors can all belong to this process while still owning unrelated input.
/// </summary>
internal static class PresentationInputFocusPolicy
{
    internal static bool IsAuthorizedForeground(
        IntPtr foregroundWindow,
        IntPtr overlayWindow,
        IntPtr toolbarWindow)
    {
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        return foregroundWindow == overlayWindow
               || foregroundWindow == toolbarWindow;
    }
}

internal sealed class PresentationInputPipeline
{
    private readonly PresentationControlService _presentationService;

    public PresentationInputPipeline(
        PresentationControlService presentationService,
        InputStrategy wpsStrategy = InputStrategy.Auto,
        InputStrategy officeStrategy = InputStrategy.Auto)
    {
        _presentationService = presentationService ?? throw new ArgumentNullException(nameof(presentationService));
        WpsStrategy = wpsStrategy;
        OfficeStrategy = officeStrategy;
    }

    public InputStrategy WpsStrategy { get; private set; }
    public InputStrategy OfficeStrategy { get; private set; }
    public bool WpsForceMessageFallback { get; private set; }

    public void UpdateWpsMode(string mode)
    {
        WpsStrategy = ResolveInputStrategyMode(mode);
        _presentationService.ResetWpsAutoFallback();
        _presentationService.ResetOfficeAutoFallback();
        WpsForceMessageFallback = false;
    }

    public void UpdateOfficeMode(string mode)
    {
        OfficeStrategy = ResolveInputStrategyMode(mode);
        _presentationService.ResetOfficeAutoFallback();
    }

    public void ResetAutoFallbacks()
    {
        _presentationService.ResetWpsAutoFallback();
        _presentationService.ResetOfficeAutoFallback();
    }

    public void ResetOfficeAutoFallback()
    {
        _presentationService.ResetOfficeAutoFallback();
    }

    public void ResetWpsHookFallback()
    {
        WpsForceMessageFallback = false;
    }

    public void MarkWpsHookUnavailable()
    {
        WpsForceMessageFallback = true;
    }

    public InputStrategy ResolveWpsSendMode(bool targetIsValid, IntPtr targetHandle = default)
    {
        if (WpsForceMessageFallback)
        {
            return InputStrategy.Message;
        }

        if (WpsStrategy == InputStrategy.Auto)
        {
            var forceMessage = targetHandle == IntPtr.Zero
                ? _presentationService.IsWpsAutoForcedMessage
                : _presentationService.IsWpsAutoForcedMessageForTarget(targetHandle);
            if (forceMessage)
            {
                return InputStrategy.Message;
            }

            return targetIsValid
                ? InputStrategy.Raw
                : InputStrategy.Message;
        }

        return WpsStrategy;
    }

    public PresentationControlOptions BuildWpsOptions(
        PresentationControlOptions currentOptions,
        string? source = null,
        bool allowBackground = false)
    {
        if (currentOptions == null)
        {
            return new PresentationControlOptions
            {
                Strategy = InputStrategy.Message,
                AllowBackground = allowBackground,
                AllowOffice = false,
                AllowWps = true
            };
        }

        var strategy = ResolveWpsOptionStrategy(currentOptions, source, allowBackground);
        return new PresentationControlOptions
        {
            Strategy = strategy,
            WheelAsKey = currentOptions.WheelAsKey,
            AllowBackground = allowBackground,
            WpsDebounceMs = currentOptions.WpsDebounceMs,
            LockStrategyWhenDegraded = currentOptions.LockStrategyWhenDegraded,
            AutoFallbackFailureThreshold = currentOptions.AutoFallbackFailureThreshold,
            AutoFallbackProbeIntervalCommands = currentOptions.AutoFallbackProbeIntervalCommands,
            AllowOffice = false,
            AllowWps = true
        };
    }

    public PresentationControlOptions BuildOfficeOptions(
        PresentationControlOptions currentOptions,
        bool allowBackground = false)
    {
        if (currentOptions == null)
        {
            return new PresentationControlOptions
            {
                Strategy = allowBackground ? InputStrategy.Message : OfficeStrategy,
                AllowBackground = allowBackground,
                AllowOffice = true,
                AllowWps = false
            };
        }

        return new PresentationControlOptions
        {
            Strategy = allowBackground ? InputStrategy.Message : OfficeStrategy,
            WheelAsKey = currentOptions.WheelAsKey,
            AllowBackground = allowBackground,
            WpsDebounceMs = currentOptions.WpsDebounceMs,
            LockStrategyWhenDegraded = currentOptions.LockStrategyWhenDegraded,
            AutoFallbackFailureThreshold = currentOptions.AutoFallbackFailureThreshold,
            AutoFallbackProbeIntervalCommands = currentOptions.AutoFallbackProbeIntervalCommands,
            AllowOffice = true,
            AllowWps = false
        };
    }

    public static InputStrategy ResolveInputStrategyMode(string mode)
    {
        return mode switch
        {
            WpsInputModeDefaults.Raw => InputStrategy.Raw,
            WpsInputModeDefaults.Message => InputStrategy.Message,
            _ => InputStrategy.Auto
        };
    }

    private InputStrategy ResolveWpsOptionStrategy(
        PresentationControlOptions currentOptions,
        string? source,
        bool allowBackground)
    {
        var strategy = WpsStrategy;
        if (allowBackground || WpsForceMessageFallback)
        {
            strategy = InputStrategy.Message;
        }
        if (IsWpsHookNavigationSource(source) && currentOptions.WheelAsKey)
        {
            strategy = InputStrategy.Message;
        }

        return strategy;
    }

    private static bool IsWpsHookNavigationSource(string? source)
    {
        return string.Equals(source, "wheel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "keyboard", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class PresentationKeyCommandPolicy
{
    internal static bool TryMap(Key key, out PresentationCommand command)
    {
        if (key == Key.Right || key == Key.Down || key == Key.Space || key == Key.Enter || key == Key.PageDown)
        {
            command = PresentationCommand.Next;
            return true;
        }
        if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            command = PresentationCommand.Previous;
            return true;
        }
        if (key == Key.Home)
        {
            command = PresentationCommand.First;
            return true;
        }
        if (key == Key.End)
        {
            command = PresentationCommand.Last;
            return true;
        }

        command = default;
        return false;
    }
}

internal static class PresentationNavigationAdmissionPolicy
{
    internal static bool ShouldAttempt(
        bool allowChannel,
        bool boardActive,
        bool targetIsValid,
        bool targetHasInfo,
        bool targetIsSlideshow,
        bool allowBackground,
        bool targetForeground)
    {
        if (!allowChannel || boardActive)
        {
            return false;
        }

        if (!targetIsValid || !targetHasInfo || !targetIsSlideshow)
        {
            return false;
        }

        if (!allowBackground && !targetForeground)
        {
            return false;
        }

        return true;
    }
}

internal static class PresentationOverlayRetouchPolicy
{
    internal static bool ShouldRequest(
        bool presentationActionApplied,
        bool overlayVisible,
        bool presentationFullscreenActive)
    {
        return presentationActionApplied
            && overlayVisible
            && presentationFullscreenActive;
    }
}

internal static class PresentationReservedNavigationKeyPolicy
{
    private static readonly IReadOnlyCollection<VirtualKey> Empty = Array.Empty<VirtualKey>();

    internal static IReadOnlyCollection<VirtualKey> ResolveRollCallGroupSwitchKeys(
        bool enabled,
        string? configuredKey)
    {
        if (!enabled)
        {
            return Empty;
        }

        var token = string.IsNullOrWhiteSpace(configuredKey)
            ? "enter"
            : configuredKey.Trim();
        return KeyBindingParser.TryParse(token, out var binding) && binding != null
            ? [binding.Key]
            : Empty;
    }
}

internal static class PresentationRuntimeDefaults
{
    internal const int FocusMonitorIntervalMs = 500;
    internal const int FocusRestoreCooldownMs = 1200;
    internal const int WpsNavDebounceMs = 200;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}

internal static class PresentationSlideshowDetectionPolicy
{
    internal static bool IsSlideshow(
        PresentationTarget target,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow,
        PresentationType? expectedType = null)
    {
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }

        if (classifier.IsSlideshowWindow(target.Info))
        {
            return true;
        }

        if (!isFullscreenWindow(target.Handle))
        {
            return false;
        }

        var type = expectedType ?? classifier.Classify(target.Info);
        return PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: type == PresentationType.Office,
            classifiesAsDedicatedWpsRuntime: type == PresentationType.Wps
                && WpsPresentationRuntimePolicy.IsDedicatedSlideshowRuntime(target.Info.ProcessName));
    }
}

internal static class PresentationTargetAdmissionPolicy
{
    internal static bool IsFreshIdentityMatch(
        PresentationTarget target,
        PresentationWindowCheck? currentCheck,
        PresentationClassifier classifier,
        PresentationType? expectedType)
    {
        ArgumentNullException.ThrowIfNull(classifier);

        if (!target.IsValid || target.Info == null || currentCheck == null)
        {
            return false;
        }

        if (currentCheck.Type is PresentationType.None or PresentationType.Other)
        {
            return false;
        }

        // HWND values can be recycled after a window closes.  The process id,
        // executable name, and native class identity must all still describe
        // the cached target before input is admitted.
        if (target.Info.ProcessId == 0
            || currentCheck.ProcessId == 0
            || target.Info.ProcessId != currentCheck.ProcessId
            || !string.Equals(
                NormalizeIdentity(target.Info.ProcessName),
                NormalizeIdentity(currentCheck.ProcessName),
                StringComparison.OrdinalIgnoreCase)
            || !ClassIdentityMatches(target.Info.ClassNames, currentCheck.ClassNames))
        {
            return false;
        }

        // The cached PresentationTarget carries the metadata used by the planner.
        // A recycled HWND must not be allowed to keep the old channel identity.
        var cachedType = classifier.Classify(target.Info);
        if (currentCheck.Type != cachedType)
        {
            return false;
        }

        if (expectedType.HasValue && currentCheck.Type != expectedType.Value)
        {
            return false;
        }

        if (currentCheck.ClassMatch)
        {
            return true;
        }

        return PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: target.IsValid,
            targetHasInfo: target.Info != null,
            isFullscreen: currentCheck.IsFullscreen,
            classifiesAsSlideshow: false,
            classifiesAsOffice: currentCheck.Type == PresentationType.Office,
            classifiesAsDedicatedWpsRuntime: currentCheck.Type == PresentationType.Wps
                && WpsPresentationRuntimePolicy.IsDedicatedSlideshowRuntime(currentCheck.ProcessName));
    }

    private static bool ClassIdentityMatches(
        IReadOnlyList<string>? cachedClasses,
        IReadOnlyList<string>? currentClasses)
    {
        var cached = NormalizeClasses(cachedClasses);
        var current = NormalizeClasses(currentClasses);
        return cached.Count > 0
            && cached.Count == current.Count
            && cached.SetEquals(current);
    }

    private static HashSet<string> NormalizeClasses(IReadOnlyList<string>? classes)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (classes == null)
        {
            return normalized;
        }

        for (var i = 0; i < classes.Count; i++)
        {
            var value = NormalizeIdentity(classes[i]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private static string NormalizeIdentity(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

internal static class PresentationTargetChannelSelectionPolicy
{
    internal static PresentationType ResolveForFocus(
        PresentationType foregroundType,
        bool foregroundIsFullscreen,
        PresentationType currentPresentationType,
        bool allowWps,
        bool allowOffice)
    {
        if (foregroundIsFullscreen && IsAllowed(foregroundType, allowWps, allowOffice))
        {
            return foregroundType;
        }

        if (IsAllowed(currentPresentationType, allowWps, allowOffice))
        {
            return currentPresentationType;
        }

        if (allowWps && !allowOffice)
        {
            return PresentationType.Wps;
        }

        if (allowOffice && !allowWps)
        {
            return PresentationType.Office;
        }

        // With both channels enabled and no foreground/current-session evidence,
        // do not silently choose one application over the other.
        return PresentationType.None;
    }

    private static bool IsAllowed(
        PresentationType type,
        bool allowWps,
        bool allowOffice)
    {
        return (type == PresentationType.Wps && allowWps)
            || (type == PresentationType.Office && allowOffice);
    }
}

/// <summary>
/// Keeps a presentation channel bound to one admitted HWND for the lifetime of
/// the current presentation session.  Window enumeration remains the recovery
/// path, not the normal destination selection path for every input event.
/// </summary>
internal sealed class PresentationTargetSessionBinding
{
    private readonly object _sync = new();
    private PresentationTarget _wpsTarget = PresentationTarget.Empty;
    private PresentationTarget _officeTarget = PresentationTarget.Empty;

    internal PresentationTarget Resolve(
        PresentationType type,
        Func<PresentationTarget> resolveCandidate,
        Func<PresentationTarget, bool> isAdmitted,
        PresentationTarget? preferredTarget = null)
    {
        ArgumentNullException.ThrowIfNull(resolveCandidate);
        ArgumentNullException.ThrowIfNull(isAdmitted);

        if (preferredTarget?.IsValid == true)
        {
            InvalidateIfBoundToDifferentWindow(type, preferredTarget.Handle);
        }

        lock (_sync)
        {
            var bound = Get(type);
            if (bound.IsValid && isAdmitted(bound))
            {
                return bound;
            }

            Set(type, PresentationTarget.Empty);
            var candidate = preferredTarget?.IsValid == true && isAdmitted(preferredTarget)
                ? preferredTarget
                : resolveCandidate();
            if (!candidate.IsValid || !isAdmitted(candidate))
            {
                return PresentationTarget.Empty;
            }

            Set(type, candidate);
            return candidate;
        }
    }

    internal void Invalidate(PresentationType type)
    {
        lock (_sync)
        {
            Set(type, PresentationTarget.Empty);
        }
    }

    internal bool InvalidateIfBoundToDifferentWindow(PresentationType type, IntPtr activeWindow)
    {
        if (activeWindow == IntPtr.Zero)
        {
            return false;
        }

        lock (_sync)
        {
            var bound = Get(type);
            if (!bound.IsValid || bound.Handle == activeWindow)
            {
                return false;
            }

            Set(type, PresentationTarget.Empty);
            return true;
        }
    }

    internal void InvalidateAll()
    {
        lock (_sync)
        {
            _wpsTarget = PresentationTarget.Empty;
            _officeTarget = PresentationTarget.Empty;
        }
    }

    private PresentationTarget Get(PresentationType type)
    {
        return type switch
        {
            PresentationType.Wps => _wpsTarget,
            PresentationType.Office => _officeTarget,
            _ => PresentationTarget.Empty
        };
    }

    private void Set(PresentationType type, PresentationTarget target)
    {
        switch (type)
        {
            case PresentationType.Wps:
                _wpsTarget = target;
                break;
            case PresentationType.Office:
                _officeTarget = target;
                break;
        }
    }
}

internal static class PresentationWheelInkConflictPolicy
{
    internal static bool ShouldSuppress(
        PaintToolMode mode,
        DateTime lastInkInputUtc,
        DateTime nowUtc,
        int suppressWindowMs)
    {
        if (mode == PaintToolMode.Cursor)
        {
            return false;
        }

        if (lastInkInputUtc == InkRuntimeTimingDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        var windowMs = Math.Max(0, suppressWindowMs);
        return (nowUtc - lastInkInputUtc).TotalMilliseconds < windowMs;
    }
}
