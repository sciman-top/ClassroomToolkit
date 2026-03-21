using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct PresentationNavigationDecision(
    bool ShouldDispatch,
    bool ShouldSuppressAsDebounced,
    InputStrategy Strategy,
    string Reason)
{
    internal static PresentationNavigationDecision Dispatch(
        InputStrategy strategy,
        bool suppressAsDebounced,
        string reason)
    {
        return new PresentationNavigationDecision(
            ShouldDispatch: !suppressAsDebounced,
            ShouldSuppressAsDebounced: suppressAsDebounced,
            Strategy: strategy,
            Reason: reason ?? string.Empty);
    }

    internal static PresentationNavigationDecision SuppressAsDebounced(string reason)
    {
        return new PresentationNavigationDecision(
            ShouldDispatch: false,
            ShouldSuppressAsDebounced: true,
            Strategy: InputStrategy.Message,
            Reason: reason ?? string.Empty);
    }
}
