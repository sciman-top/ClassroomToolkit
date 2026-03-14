using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanTerminationPolicy
{
    internal static bool ShouldEndPan(
        bool shouldAllowPhotoPan,
        MouseButtonState leftButton,
        MouseButtonState rightButton)
    {
        if (!shouldAllowPhotoPan)
        {
            return true;
        }

        return leftButton != MouseButtonState.Pressed
               && rightButton != MouseButtonState.Pressed;
    }
}
