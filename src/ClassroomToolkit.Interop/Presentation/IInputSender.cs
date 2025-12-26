namespace ClassroomToolkit.Interop.Presentation;

public interface IInputSender
{
    bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy);
}
