namespace ClassroomToolkit.Interop.Presentation;

internal static class WpsHookKeyboardInjectionPolicy
{
    private const uint LlkhfInjected = 0x10;
    private const uint LlkhfLowerIlInjected = 0x02;

    internal static bool ShouldIgnore(uint flags)
    {
        return (flags & (LlkhfInjected | LlkhfLowerIlInjected)) != 0;
    }
}

