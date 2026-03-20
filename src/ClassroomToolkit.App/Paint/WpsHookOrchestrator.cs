using System.Diagnostics;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct WpsHookRuntimeState(
    bool IsActive,
    bool BlockOnly,
    bool InterceptKeyboard,
    bool InterceptWheel);

internal sealed class WpsHookOrchestrator
{
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

        hookClient.SetInterceptEnabled(true);
        hookClient.SetBlockOnly(decision.BlockOnly);
        hookClient.SetInterceptKeyboard(decision.InterceptKeyboard);
        hookClient.SetInterceptWheel(decision.InterceptWheel);
        hookClient.SetEmitWheelOnBlock(decision.EmitWheelOnBlock);

        return new WpsHookRuntimeState(
            IsActive: currentActive,
            BlockOnly: decision.BlockOnly,
            InterceptKeyboard: decision.InterceptKeyboard,
            InterceptWheel: decision.InterceptWheel);
    }

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

        hookClient.SetInterceptEnabled(false);
        hookClient.SetBlockOnly(false);
        hookClient.SetInterceptKeyboard(true);
        hookClient.SetInterceptWheel(true);
        hookClient.SetEmitWheelOnBlock(true);
        hookClient.Stop();

        return new WpsHookRuntimeState(
            IsActive: false,
            BlockOnly: false,
            InterceptKeyboard: true,
            InterceptWheel: true);
    }

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
}
