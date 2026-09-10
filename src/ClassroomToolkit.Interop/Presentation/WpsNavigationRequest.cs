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
