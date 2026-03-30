namespace ClassroomToolkit.Interop.Presentation;

public interface IInputSender
{
    bool SendKey(IntPtr hwnd, VirtualKey key, KeyModifiers modifiers, InputStrategy strategy, bool keyDownOnly);
    bool SendWheel(IntPtr hwnd, int delta, InputStrategy strategy);
}
