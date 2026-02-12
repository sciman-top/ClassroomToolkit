using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationCommandMapper
{
    public KeyBinding Map(PresentationType _, PresentationCommand command)
    {
        return command switch
        {
            PresentationCommand.Next => MapNext(),
            PresentationCommand.Previous => MapPrevious(),
            PresentationCommand.First => new KeyBinding(VirtualKey.Home, KeyModifiers.None),
            PresentationCommand.Last => new KeyBinding(VirtualKey.End, KeyModifiers.None),
            PresentationCommand.BlackScreenToggle => new KeyBinding(VirtualKey.B, KeyModifiers.None),
            PresentationCommand.WhiteScreenToggle => new KeyBinding(VirtualKey.W, KeyModifiers.None),
            _ => new KeyBinding(VirtualKey.Tab, KeyModifiers.None)
        };
    }

    private static KeyBinding MapNext()
    {
        return new KeyBinding(VirtualKey.PageDown, KeyModifiers.None);
    }

    private static KeyBinding MapPrevious()
    {
        return new KeyBinding(VirtualKey.PageUp, KeyModifiers.None);
    }
}
