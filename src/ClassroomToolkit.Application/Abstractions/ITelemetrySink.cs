namespace ClassroomToolkit.Application.Abstractions;

public interface ITelemetrySink
{
    void Track(string name, IReadOnlyDictionary<string, object?>? properties = null);
}
