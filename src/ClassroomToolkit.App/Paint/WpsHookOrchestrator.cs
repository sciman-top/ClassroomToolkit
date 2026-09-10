using ClassroomToolkit.Interop.Presentation;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ClassroomToolkit.App.Paint;

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
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
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
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
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
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PaintOverlay] Failed to {operationName} WPS hook: {ex.Message}");
            return false;
        }
    }
}
