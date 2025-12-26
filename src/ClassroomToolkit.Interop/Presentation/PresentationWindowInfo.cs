namespace ClassroomToolkit.Interop.Presentation;

public sealed record PresentationWindowInfo(uint ProcessId, string ProcessName, IReadOnlyList<string> ClassNames)
{
    public static PresentationWindowInfo FromProcess(string processName)
    {
        return new PresentationWindowInfo(0, processName, Array.Empty<string>());
    }
}
