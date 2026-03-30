namespace ClassroomToolkit.App.Settings;

public interface IConfigurationService
{
    string BaseDirectory { get; }

    string SettingsIniPath { get; }

    SettingsDocumentFormat SettingsDocumentFormat { get; }

    string SettingsDocumentPath { get; }
}
