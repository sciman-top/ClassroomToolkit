
namespace ClassroomToolkit.Interop.Presentation;

/// <summary>
/// Captures the focus context at the low-level hook boundary.  A queued
/// navigation request must not be reinterpreted after focus has moved to a
/// different window.
/// </summary>
public readonly record struct WpsNavigationRequest(
    int Direction,
    string Source,
    IntPtr ForegroundWindow,
    long CapturedTimestampTicks);

internal static class WpsHookKeyboardInjectionPolicy
{
    private const uint LlkhfInjected = 0x10;
    private const uint LlkhfLowerIlInjected = 0x02;

    internal static bool ShouldIgnore(uint flags)
    {
        return (flags & (LlkhfInjected | LlkhfLowerIlInjected)) != 0;
    }
}
