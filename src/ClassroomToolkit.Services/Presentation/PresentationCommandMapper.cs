using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationCommandMapper
{
    public KeyBinding Map(PresentationType type, PresentationCommand command)
    {
        return command switch
        {
            PresentationCommand.Next => MapNext(type),
            PresentationCommand.Previous => MapPrevious(type),
            PresentationCommand.BlackScreenToggle => new KeyBinding(VirtualKey.B, KeyModifiers.None),
            PresentationCommand.WhiteScreenToggle => new KeyBinding(VirtualKey.W, KeyModifiers.None),
            _ => new KeyBinding(VirtualKey.Tab, KeyModifiers.None)
        };
    }

    private static KeyBinding MapNext(PresentationType type)
    {
        return new KeyBinding(VirtualKey.PageDown, KeyModifiers.None);
    }

    private static KeyBinding MapPrevious(PresentationType type)
    {
        return new KeyBinding(VirtualKey.PageUp, KeyModifiers.None);
    }
}
