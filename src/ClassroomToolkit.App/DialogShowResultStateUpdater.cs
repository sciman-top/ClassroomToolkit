namespace ClassroomToolkit.App;

internal static class DialogShowResultStateUpdater
{
    internal static void MarkFromDialogResult(
        ref bool result,
        bool? dialogResult)
    {
        result = dialogResult == true;
    }
}
