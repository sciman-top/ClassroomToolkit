using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

public sealed class PresentationCommandMapper
{
    public KeyBinding Map(PresentationType _, PresentationCommand command)
    {
        return command switch
        {
            PresentationCommand.Next => new KeyBinding(VirtualKey.PageDown, KeyModifiers.None),
            PresentationCommand.Previous => new KeyBinding(VirtualKey.PageUp, KeyModifiers.None),
            PresentationCommand.First => new KeyBinding(VirtualKey.Home, KeyModifiers.None),
            PresentationCommand.Last => new KeyBinding(VirtualKey.End, KeyModifiers.None),
            PresentationCommand.BlackScreenToggle => new KeyBinding(VirtualKey.B, KeyModifiers.None),
            PresentationCommand.WhiteScreenToggle => new KeyBinding(VirtualKey.W, KeyModifiers.None),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported presentation command.")
        };
    }
}
