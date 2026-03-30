namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePointerUpRefreshSourcePolicy
{
    internal const string PointerUp = "pointer-up";
    internal const string PointerUpInk = "pointer-up-ink";

    internal static string Resolve(bool hadInkOperation)
    {
        return hadInkOperation ? PointerUpInk : PointerUp;
    }
}
