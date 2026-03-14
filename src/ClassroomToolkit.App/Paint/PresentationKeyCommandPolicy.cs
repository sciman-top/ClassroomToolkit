using System.Windows.Input;
using ClassroomToolkit.Services.Presentation;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationKeyCommandPolicy
{
    internal static bool TryMap(Key key, out PresentationCommand command)
    {
        if (key == Key.Right || key == Key.Down || key == Key.Space || key == Key.Enter || key == Key.PageDown)
        {
            command = PresentationCommand.Next;
            return true;
        }
        if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            command = PresentationCommand.Previous;
            return true;
        }
        if (key == Key.Home)
        {
            command = PresentationCommand.First;
            return true;
        }
        if (key == Key.End)
        {
            command = PresentationCommand.Last;
            return true;
        }

        command = default;
        return false;
    }
}
