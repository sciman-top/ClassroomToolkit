namespace ClassroomToolkit.App.Windowing;

internal enum FloatingDispatchExecuteAdmissionReason
{
    None = 0,
    ApplyQueued = 1,
    NotQueued = 2
}

internal readonly record struct FloatingDispatchExecuteAdmissionDecision(
    bool ShouldExecute,
    FloatingDispatchExecuteAdmissionReason Reason);

internal static class FloatingDispatchExecuteAdmissionPolicy
{
    internal static FloatingDispatchExecuteAdmissionDecision Resolve(bool applyQueued)
    {
        return applyQueued
            ? new FloatingDispatchExecuteAdmissionDecision(
                ShouldExecute: true,
                Reason: FloatingDispatchExecuteAdmissionReason.ApplyQueued)
            : new FloatingDispatchExecuteAdmissionDecision(
                ShouldExecute: false,
                Reason: FloatingDispatchExecuteAdmissionReason.NotQueued);
    }
}
