namespace ClassroomToolkit.App.Paint;

internal static class PresentationKeyboardDispatchPolicy
{
    internal static bool ShouldDispatch(bool presentationAllowed, bool keyMapped)
    {
        return presentationAllowed && keyMapped;
    }
}
