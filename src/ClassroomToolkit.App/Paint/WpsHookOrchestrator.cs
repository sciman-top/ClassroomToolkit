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
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance API kept for compatibility with existing tests and call sites.")]
    public WpsHookRuntimeState ApplyEnabled(
        IWpsNavHookClient? hookClient,
        WpsHookInterceptDecision decision,
        bool currentActive)
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
            hookClient.SetInterceptEnabled(true);
            hookClient.SetBlockOnly(decision.BlockOnly);
            hookClient.SetInterceptKeyboard(decision.InterceptKeyboard);
            hookClient.SetInterceptWheel(decision.InterceptWheel);
            hookClient.SetEmitWheelOnBlock(decision.EmitWheelOnBlock);
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
            & TryApply(() => hookClient.SetSuppressedKeyboardKeys([]), "clear-suppressed-keys")
            & TryApply(hookClient.Stop, "stop");

        return new WpsHookRuntimeState(
            IsActive: false,
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
            return await hookClient.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PaintOverlay] Failed to start WPS hook: {ex.Message}");
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
