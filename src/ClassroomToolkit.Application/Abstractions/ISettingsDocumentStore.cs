namespace ClassroomToolkit.Application.Abstractions;

public interface ISettingsDocumentStore
{
    bool IsOverwriteBlocked => false;
    Dictionary<string, Dictionary<string, string>> Load();
    void Save(Dictionary<string, Dictionary<string, string>> data);
}
