namespace ClassroomToolkit.Application.Abstractions;

public interface ISettingsDocumentStore
{
    Dictionary<string, Dictionary<string, string>> Load();
    void Save(Dictionary<string, Dictionary<string, string>> data);
}
