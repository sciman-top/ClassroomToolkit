using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Threading;
using System;
using WpfWindow
=
System.Windows.Window;

namespace ClassroomToolkit.App.Paint;

internal interface IWpsNavHookClient
{
    bool Available { get; }
    bool IsActive { get; }
    void SetInterceptEnabled(bool enabled);
    void SetBlockOnly(bool enabled);
    void SetInterceptKeyboard(bool enabled);
    void SetInterceptWheel(bool enabled);
    void SetEmitWheelOnBlock(bool enabled);
    void SetConsumeAuthorizedInput(bool enabled);
    void SetAuthorizedInputWindows(IEnumerable<IntPtr> windows);
    void SetSuppressedKeyboardKeys(IEnumerable<VirtualKey> keys);
    Task<bool> StartAsync();
    void Stop();
}

internal sealed class WpsNavHookClient : IWpsNavHookClient
{
    private readonly WpsSlideshowNavigationHook _hook;

    public WpsNavHookClient(WpsSlideshowNavigationHook hook)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
    }

    public bool Available => _hook.Available;
    public bool IsActive => _hook.IsActive;

    public void SetInterceptEnabled(bool enabled) => _hook.SetInterceptEnabled(enabled);
    public void SetBlockOnly(bool enabled) => _hook.SetBlockOnly(enabled);
    public void SetInterceptKeyboard(bool enabled) => _hook.SetInterceptKeyboard(enabled);
    public void SetInterceptWheel(bool enabled) => _hook.SetInterceptWheel(enabled);
    public void SetEmitWheelOnBlock(bool enabled) => _hook.SetEmitWheelOnBlock(enabled);
    public void SetConsumeAuthorizedInput(bool enabled) => _hook.SetConsumeAuthorizedInput(enabled);
    public void SetAuthorizedInputWindows(IEnumerable<IntPtr> windows) =>
        _hook.SetAuthorizedInputWindows(windows);
    public void SetSuppressedKeyboardKeys(IEnumerable<VirtualKey> keys) =>
        _hook.SetSuppressedKeyboardKeys(keys);
    public Task<bool> StartAsync() => _hook.StartAsync();
    public void Stop() => _hook.Stop();
}

internal static class MessageBoxWpsHookUnavailableNotifier
{
    private const string Message = "检测到 WPS 放映全局钩子不可用，已自动切换为消息投递模式。";
    private const string Title = "提示";

    internal static void Notify(WpfWindow fallbackOwner)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        TopmostMessageBox.Show(owner ?? fallbackOwner, Message, Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }
}

internal sealed record WpsHookInterceptDecision(
    bool InterceptKeyboard,
    bool InterceptWheel,
    bool BlockOnly,
    bool EmitWheelOnBlock);

internal readonly record struct WpsHookRuntimeState(
    bool IsActive,
    bool BlockOnly,
    bool InterceptKeyboard,
    bool InterceptWheel,
    bool ConfigurationApplied = true);

internal sealed class WpsHookOrchestrator
{
    // 保留键（点名分组切换键）的属主缓存：ApplyDisabled 会清空钩子侧保留键，
    // ApplyDisabled→ApplyEnabled 循环（overlay 显隐、板书/照片模式切换）必须由这里回填，
    // 否则保留键静默丢失，Enter 会同时切换分组并注入 WPS 翻页。
    private VirtualKey[] _reservedPresentationKeys = [];

    public void SetReservedPresentationKeys(IEnumerable<VirtualKey>? keys)
    {
        _reservedPresentationKeys = keys?.ToArray() ?? [];
    }

    public WpsHookRuntimeState ApplyEnabled(
        IWpsNavHookClient? hookClient,
        WpsHookInterceptDecision decision,
        bool currentActive,
        IEnumerable<IntPtr>? authorizedInputWindows = null)
    {
        if (hookClient == null)
        {
            return new WpsHookRuntimeState(
                IsActive: currentActive,
                BlockOnly: decision.BlockOnly,
                InterceptKeyboard: decision.InterceptKeyboard,
                InterceptWheel: decision.InterceptWheel);
        }

        try
        {
            hookClient.SetAuthorizedInputWindows(authorizedInputWindows ?? []);
            hookClient.SetConsumeAuthorizedInput(true);
            hookClient.SetInterceptEnabled(true);
            hookClient.SetBlockOnly(decision.BlockOnly);
            hookClient.SetInterceptKeyboard(decision.InterceptKeyboard);
            hookClient.SetInterceptWheel(decision.InterceptWheel);
            hookClient.SetEmitWheelOnBlock(decision.EmitWheelOnBlock);
            if (_reservedPresentationKeys.Length > 0)
            {
                hookClient.SetSuppressedKeyboardKeys(_reservedPresentationKeys);
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PaintOverlay] Failed to configure WPS hook: {ex.Message}");
            var disabledState = ApplyDisabled(hookClient);
            return disabledState with { ConfigurationApplied = false };
        }

        return new WpsHookRuntimeState(
            IsActive: currentActive,
            BlockOnly: decision.BlockOnly,
            InterceptKeyboard: decision.InterceptKeyboard,
            InterceptWheel: decision.InterceptWheel);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance API kept for compatibility with existing tests and call sites.")]
    public WpsHookRuntimeState ApplyDisabled(IWpsNavHookClient? hookClient)
    {
        if (hookClient == null)
        {
            return new WpsHookRuntimeState(
                IsActive: false,
                BlockOnly: false,
                InterceptKeyboard: true,
                InterceptWheel: true);
        }

        // Use non-short-circuit '&' so one failed reset does not prevent the final Stop attempt.
        var configurationApplied =
            TryApply(() => hookClient.SetInterceptEnabled(false), "disable-intercept")
            & TryApply(() => hookClient.SetBlockOnly(false), "disable-block-only")
            & TryApply(() => hookClient.SetInterceptKeyboard(true), "reset-keyboard-intercept")
            & TryApply(() => hookClient.SetInterceptWheel(true), "reset-wheel-intercept")
            & TryApply(() => hookClient.SetEmitWheelOnBlock(true), "reset-wheel-emission")
            & TryApply(() => hookClient.SetConsumeAuthorizedInput(false), "reset-authorized-input-consumption")
            & TryApply(() => hookClient.SetAuthorizedInputWindows([]), "clear-authorized-input-windows")
            & TryApply(() => hookClient.SetSuppressedKeyboardKeys([]), "clear-suppressed-keys")
            & TryApply(hookClient.Stop, "stop")
            & !hookClient.IsActive;

        return new WpsHookRuntimeState(
            IsActive: hookClient.IsActive,
            BlockOnly: false,
            InterceptKeyboard: true,
            InterceptWheel: true,
            ConfigurationApplied: configurationApplied);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance API kept for compatibility with existing tests and call sites.")]
    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Caller resumes on the UI thread by design: the LL WPS hook must be installed on the UI thread (see PaintOverlayWindow.Presentation.WpsHook.cs).")]
    public async Task<bool> TryStartSafeAsync(IWpsNavHookClient? hookClient)
    {
        if (hookClient == null || !hookClient.Available)
        {
            return false;
        }

        try
        {
            return await hookClient.StartAsync();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PaintOverlay] Failed to start WPS hook: {ex.Message}");
            _ = ApplyDisabled(hookClient);
            return false;
        }
    }

    private static bool TryApply(Action operation, string operationName)
    {
        try
        {
            operation();
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PaintOverlay] Failed to {operationName} WPS hook: {ex.Message}");
            return false;
        }
    }
}

internal readonly record struct WpsNavigationDebounceState(
    (int Code, IntPtr Target, DateTime Timestamp)? LastEvent);

internal static class WpsNavigationDebounceStateUpdater
{
    internal static void Apply(
        ref (int Code, IntPtr Target, DateTime Timestamp)? lastEvent,
        WpsNavigationDebounceState state)
    {
        lastEvent = state.LastEvent;
    }
}

internal static class WpsHookPolicies
{
    internal static bool ShouldTreatAsActiveFullscreen(
        bool hasFullscreenCandidate,
        PresentationType foregroundType,
        bool foregroundIsFullscreen,
        bool foregroundOwnedByCurrentProcess)
    {
        if (!hasFullscreenCandidate)
        {
            return false;
        }

        if (foregroundOwnedByCurrentProcess)
        {
            return true;
        }

        // WPS may leave a background fullscreen candidate alive briefly after exit.
        // If foreground already returned to a non-fullscreen WPS window, treat slideshow as ended.
        if (foregroundType == PresentationType.Wps && !foregroundIsFullscreen)
        {
            return false;
        }

        return true;
    }

    internal static bool ShouldAttemptResolveTarget(
        bool allowWps,
        bool boardActive,
        bool overlayVisible,
        bool photoModeActive)
    {
        if (!allowWps || boardActive || !overlayVisible || photoModeActive)
        {
            return false;
        }

        return true;
    }

    internal static bool ShouldEnableWithTarget(
        bool shouldAttemptResolveTarget,
        bool targetValid,
        bool targetIsSlideshow)
    {
        if (!shouldAttemptResolveTarget)
        {
            return false;
        }

        return targetValid && targetIsSlideshow;
    }

    internal static bool IsRecent(
        DateTime lastHookInputUtc,
        DateTime nowUtc,
        int debounceMs)
    {
        if (lastHookInputUtc == PresentationRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        return (nowUtc - lastHookInputUtc).TotalMilliseconds < debounceMs;
    }

    internal static WpsHookInterceptDecision Resolve(
        bool shouldEnable,
        PaintToolMode mode,
        bool targetIsSlideshow,
        bool targetForeground,
        bool isRawSendMode,
        bool wheelForward)
    {
        var blockOnly = false;
        var interceptKeyboard = true;
        // When WPS already owns the foreground, keep its native wheel path and
        // do not also inject a key from the hook.  Wheel mapping is only a
        // background-target bridge; this removes the native-wheel + injected-key
        // double channel that debounce cannot prove away.
        var interceptWheel = wheelForward && !targetForeground;
        var emitWheelOnBlock = interceptWheel;

        if (!shouldEnable)
        {
            return new WpsHookInterceptDecision(
                InterceptKeyboard: false,
                InterceptWheel: false,
                BlockOnly: false,
                EmitWheelOnBlock: false);
        }

        if (mode == PaintToolMode.Cursor)
        {
            if (targetIsSlideshow && !targetForeground)
            {
                // Cursor mode prefers passthrough, but keep keyboard fallback
                // when WPS slideshow is not foreground to avoid navigation dead zones.
                return new WpsHookInterceptDecision(
                    InterceptKeyboard: true,
                    InterceptWheel: false,
                    BlockOnly: false,
                    EmitWheelOnBlock: false);
            }

            return new WpsHookInterceptDecision(
                InterceptKeyboard: false,
                InterceptWheel: false,
                BlockOnly: false,
                EmitWheelOnBlock: false);
        }

        // In inking mode, overlay is usually foreground. Keep hook interception
        // active for presentation scene even when target isn't foreground.
        if (!targetForeground && !targetIsSlideshow)
        {
            return new WpsHookInterceptDecision(
                InterceptKeyboard: false,
                InterceptWheel: false,
                BlockOnly: false,
                EmitWheelOnBlock: false);
        }

        if (mode != PaintToolMode.Cursor && isRawSendMode)
        {
            // In drawing mode, avoid swallowing keyboard/wheel input.
            // Keep hook for remote clickers while local input still goes through.
            blockOnly = false;
            emitWheelOnBlock = wheelForward;
        }

        return new WpsHookInterceptDecision(
            InterceptKeyboard: interceptKeyboard,
            InterceptWheel: interceptWheel,
            BlockOnly: blockOnly,
            EmitWheelOnBlock: emitWheelOnBlock);
    }

    /// <summary>
    /// 决定 LL hook 收到的导航事件是否需要由本进程再注入一次翻页命令。
    /// 只有明确授权的覆盖层/工具条前台时才允许把输入中继到后台放映窗；
    /// 外部应用及本进程其他窗口都 fail-closed。
    /// </summary>
    internal static bool ShouldSuppressInjection(
        bool targetIsForeground,
        bool foregroundInputAuthorized,
        bool wheelSource,
        bool wheelAsKeyEnabled)
    {
        if (!targetIsForeground)
        {
            // 外来应用或本进程其他窗口持有前台时，输入属于该窗口（如 Word
            // 或设置对话框中的文本输入），不得转译为后台放映翻页；只有
            // 明确授权的覆盖层/工具条 HWND 才保留翻页笔中继。
            return !foregroundInputAuthorized;
        }

        // 放映窗在前台时始终保留原生输入。WheelAsKey 只用于后台目标桥接；
        // 前台再注入会与 WPS 原生滚轮形成无法确认的双翻页。
        return true;
    }

    internal static bool IsNotified(ref int notifiedState)
    {
        return Volatile.Read(ref notifiedState) != 0;
    }

    internal static bool ShouldNotify(ref int notifiedState)
    {
        return Interlocked.Exchange(ref notifiedState, 1) == 0;
    }

    internal static void Reset(ref int notifiedState)
    {
        Interlocked.Exchange(ref notifiedState, 0);
    }

    // 仅抑制同方向+同目标的重复导航（滚轮洪泛/多路径重复触发）；
    // 反方向是用户刻意的翻页纠正，不得被全域阻断窗口吞掉。
    internal static bool ShouldSuppress(
        int direction,
        IntPtr target,
        DateTime nowUtc,
        WpsNavigationDebounceState state,
        int debounceMs)
    {
        if (target == IntPtr.Zero)
        {
            return false;
        }
        if (!state.LastEvent.HasValue)
        {
            return false;
        }

        var last = state.LastEvent.Value;
        if (last.Code != direction || last.Target != target)
        {
            return false;
        }

        return (nowUtc - last.Timestamp).TotalMilliseconds < debounceMs;
    }

    internal static WpsNavigationDebounceState Remember(
        int direction,
        IntPtr target,
        DateTime nowUtc)
    {
        return new WpsNavigationDebounceState(
            LastEvent: (direction, target, nowUtc));
    }

    internal static bool IsDedicatedSlideshowRuntime(string? processName)
    {
        return PresentationClassifier.IsDedicatedWpsPresentationRuntime(processName);
    }

    internal static bool ShouldResolveWpsRawTarget(bool presentationTargetValid, bool allowWps)
    {
        return !presentationTargetValid && allowWps;
    }

    internal static bool IsValid(bool wpsTargetValid, InputStrategy wpsSendMode)
    {
        return wpsTargetValid && wpsSendMode == InputStrategy.Raw;
    }

    internal static bool ShouldBypassDirectSend(
        bool hookActive,
        bool hookInterceptWheel,
        bool hookBlockOnly,
        bool isWpsForeground)
    {
        return hookActive
            && hookInterceptWheel
            && hookBlockOnly
            && isWpsForeground;
    }
}
