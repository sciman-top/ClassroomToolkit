namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputSwitchAdmissionPolicy
{
    internal static bool ShouldProceed(
        bool canSwitchByGate,
        bool hasBitmap,
        bool hasCurrentRect,
        bool shouldSwitchByPointer)
    {
        if (!canSwitchByGate || !hasBitmap)
        {
            return false;
        }

        return !hasCurrentRect || shouldSwitchByPointer;
    }
}
