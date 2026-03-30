using ClassroomToolkit.Interop.Presentation;

public readonly record struct KeyboardHookSuppressionDecision(
    bool ShouldSuppress,
    VirtualKey? PendingSuppressedKey);

public static class KeyboardHookSuppressionPolicy
{
    public static KeyboardHookSuppressionDecision Resolve(
        bool suppressWhenMatched,
        bool bindingMatched,
        bool isDown,
        bool isUp,
        VirtualKey key,
        VirtualKey? pendingSuppressedKey)
    {
        if (!suppressWhenMatched)
        {
            return new KeyboardHookSuppressionDecision(
                ShouldSuppress: false,
                PendingSuppressedKey: pendingSuppressedKey);
        }

        if (bindingMatched && isDown)
        {
            return new KeyboardHookSuppressionDecision(
                ShouldSuppress: true,
                PendingSuppressedKey: key);
        }

        if (isUp && pendingSuppressedKey == key)
        {
            return new KeyboardHookSuppressionDecision(
                ShouldSuppress: true,
                PendingSuppressedKey: null);
        }

        return new KeyboardHookSuppressionDecision(
            ShouldSuppress: false,
            PendingSuppressedKey: pendingSuppressedKey);
    }
}
