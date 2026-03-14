using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal enum WindowStateNormalizationReason
{
    None = 0,
    TargetMissing = 1,
    NormalizationNotRequested = 2,
    NormalizationRequested = 3
}

internal readonly record struct WindowStateNormalizationDecision(
    bool ShouldNormalize,
    WindowStateNormalizationReason Reason);

internal static class WindowStateNormalizationExecutor
{
    internal static bool Apply(Window? target, bool shouldNormalize)
    {
        var decision = Resolve(target, shouldNormalize);
        return decision.ShouldNormalize && WindowStateTransitionExecutor.Apply(target, WindowState.Normal);
    }

    internal static bool Apply<TTarget>(
        TTarget? target,
        bool shouldNormalize,
        Func<TTarget?, bool, bool> applyNormalize)
        where TTarget : class
    {
        var decision = Resolve(target, shouldNormalize);
        if (!decision.ShouldNormalize && target == null)
        {
            return false;
        }

        return applyNormalize(target, decision.ShouldNormalize);
    }

    internal static WindowStateNormalizationDecision Resolve<TTarget>(
        TTarget? target,
        bool shouldNormalize)
        where TTarget : class
    {
        if (target == null)
        {
            return new WindowStateNormalizationDecision(
                ShouldNormalize: false,
                Reason: WindowStateNormalizationReason.TargetMissing);
        }

        return shouldNormalize
            ? new WindowStateNormalizationDecision(
                ShouldNormalize: true,
                Reason: WindowStateNormalizationReason.NormalizationRequested)
            : new WindowStateNormalizationDecision(
                ShouldNormalize: false,
                Reason: WindowStateNormalizationReason.NormalizationNotRequested);
    }
}
