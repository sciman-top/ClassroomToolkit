namespace ClassroomToolkit.App.Paint;

internal static class ToolbarSecondTapIntentPolicy
{
    internal static ToolbarSecondTapTarget Resolve(
        bool alreadySelected,
        bool supportsSecondaryAction,
        ToolbarSecondTapTarget requestedTarget)
    {
        return alreadySelected && supportsSecondaryAction
            ? requestedTarget
            : ToolbarSecondTapTarget.None;
    }
}
