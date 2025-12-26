namespace ClassroomToolkit.Interop.Presentation;

public sealed record PresentationWindowInfo(string ProcessName, IReadOnlyList<string> ClassNames)
{
    public static PresentationWindowInfo FromProcess(string processName)
    {
        return new PresentationWindowInfo(processName, Array.Empty<string>());
    }
}
