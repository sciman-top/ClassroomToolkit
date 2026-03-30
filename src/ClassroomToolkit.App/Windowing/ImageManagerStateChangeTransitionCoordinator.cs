using System;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Windowing;

internal enum ImageManagerStateChangeNormalizationExecutionKind
{
    None = 0,
    Scheduled = 1,
    ImmediateFallback = 2
}

internal readonly record struct ImageManagerStateChangeTransitionExecutionResult(
    ImageManagerStateChangeNormalizationExecutionKind NormalizationExecution,
    bool AppliedSurfaceDecision);

internal static class ImageManagerStateChangeTransitionCoordinator
{
    internal static ImageManagerStateChangeTransitionExecutionResult Apply(
        ImageManagerStateChangeDecision decision,
        Action applyOverlayNormalization,
        Func<Action, bool> tryScheduleOverlayNormalization,
        Action<SurfaceZOrderDecision> applySurfaceDecision)
    {
        ArgumentNullException.ThrowIfNull(applyOverlayNormalization);
        ArgumentNullException.ThrowIfNull(tryScheduleOverlayNormalization);
        ArgumentNullException.ThrowIfNull(applySurfaceDecision);

        if (!decision.NormalizeOverlayWindowState)
        {
            return new ImageManagerStateChangeTransitionExecutionResult(
                ImageManagerStateChangeNormalizationExecutionKind.None,
                AppliedSurfaceDecision: false);
        }

        var scheduled = false;
        scheduled = SafeActionExecutionExecutor.TryExecute(
            () => tryScheduleOverlayNormalization(applyOverlayNormalization),
            fallback: false);

        if (!scheduled)
        {
            _ = SafeActionExecutionExecutor.TryExecute(applyOverlayNormalization);
        }

        var appliedSurfaceDecision = false;
        if (ImageManagerStateChangeSurfaceApplyPolicy.ShouldApply(
                decision.RequestZOrderApply,
                decision.ForceEnforceZOrder))
        {
            appliedSurfaceDecision = SafeActionExecutionExecutor.TryExecute(
                () =>
                {
                    applySurfaceDecision(ImageManagerStateChangeSurfaceDecisionPolicy.Resolve(decision));
                    return true;
                },
                fallback: false);
        }

        return new ImageManagerStateChangeTransitionExecutionResult(
            scheduled
                ? ImageManagerStateChangeNormalizationExecutionKind.Scheduled
                : ImageManagerStateChangeNormalizationExecutionKind.ImmediateFallback,
            AppliedSurfaceDecision: appliedSurfaceDecision);
    }
}
