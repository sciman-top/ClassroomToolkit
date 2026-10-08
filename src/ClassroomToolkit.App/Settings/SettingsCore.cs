
namespace ClassroomToolkit.App.Settings;

public enum SettingsDocumentFormat
{
    Ini = 0,
    Json
}

public interface IConfigurationService
{
    string BaseDirectory { get; }

    string SettingsIniPath { get; }

    SettingsDocumentFormat SettingsDocumentFormat { get; }

    string SettingsDocumentPath { get; }
}

internal static class WpsInputModeDefaults
{
    internal const string Auto = "auto";
    internal const string Raw = "raw";
    internal const string Message = "message";
}

internal readonly record struct RollCallSettingsPatch(
    bool RollCallShowId,
    bool RollCallShowName,
    bool RollCallShowPhoto,
    int RollCallPhotoDurationSeconds,
    string RollCallPhotoSharedClass,
    bool RollCallTimerSoundEnabled,
    bool RollCallTimerReminderEnabled,
    int RollCallTimerReminderIntervalMinutes,
    string RollCallTimerSoundVariant,
    string RollCallTimerReminderSoundVariant,
    bool RollCallSpeechEnabled,
    string RollCallSpeechEngine,
    string RollCallSpeechVoiceId,
    string RollCallSpeechOutputId,
    bool RollCallRemoteEnabled,
    bool RollCallRemoteGroupSwitchEnabled,
    string RemotePresenterKey,
    string RemoteGroupSwitchKey);

internal static class RollCallSettingsApplier
{
    public static void Apply(AppSettings settings, RollCallSettingsPatch patch)
    {
        settings.RollCallShowId = patch.RollCallShowId;
        settings.RollCallShowName = patch.RollCallShowName;
        settings.RollCallShowPhoto = patch.RollCallShowPhoto;
        settings.RollCallPhotoDurationSeconds = patch.RollCallPhotoDurationSeconds;
        settings.RollCallPhotoSharedClass = patch.RollCallPhotoSharedClass;
        settings.RollCallTimerSoundEnabled = patch.RollCallTimerSoundEnabled;
        settings.RollCallTimerReminderEnabled = patch.RollCallTimerReminderEnabled;
        settings.RollCallTimerReminderIntervalMinutes = patch.RollCallTimerReminderIntervalMinutes;
        settings.RollCallTimerSoundVariant = patch.RollCallTimerSoundVariant;
        settings.RollCallTimerReminderSoundVariant = patch.RollCallTimerReminderSoundVariant;
        settings.RollCallSpeechEnabled = patch.RollCallSpeechEnabled;
        settings.RollCallSpeechEngine = patch.RollCallSpeechEngine;
        settings.RollCallSpeechVoiceId = patch.RollCallSpeechVoiceId;
        settings.RollCallSpeechOutputId = patch.RollCallSpeechOutputId;
        settings.RollCallRemoteEnabled = patch.RollCallRemoteEnabled;
        settings.RollCallRemoteGroupSwitchEnabled = patch.RollCallRemoteGroupSwitchEnabled;
        settings.RemotePresenterKey = patch.RemotePresenterKey;
        settings.RemoteGroupSwitchKey = patch.RemoteGroupSwitchKey;
    }
}
