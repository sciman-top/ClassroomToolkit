using System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Presentation;

public sealed record PresentationTarget(IntPtr Handle, PresentationWindowInfo Info)
{
    public bool IsValid => Handle != IntPtr.Zero;

    public static PresentationTarget Empty => new(IntPtr.Zero, PresentationWindowInfo.FromProcess(string.Empty));

    public override string ToString()
    {
        if (Handle == IntPtr.Zero)
        {
            return "(none)";
        }
        return $"0x{Handle.ToInt64():X}";
    }
}
