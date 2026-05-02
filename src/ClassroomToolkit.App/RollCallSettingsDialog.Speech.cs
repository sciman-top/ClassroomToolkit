using System.Globalization;
using System.Speech.Synthesis;
using System.Windows;

namespace ClassroomToolkit.App;

public partial class RollCallSettingsDialog
{
    private SpeechTabState CaptureSpeechTabState()
    {
        return new SpeechTabState(
            SpeechEnabled: SpeechCheck.IsChecked == true,
            SpeechEngine: GetSelectedValue(SpeechEngineCombo, "sapi"),
            SpeechVoiceId: GetSelectedValue(SpeechVoiceCombo, string.Empty),
            SpeechOutputId: GetSelectedValue(SpeechOutputCombo, string.Empty));
    }

    private void ApplySpeechTabState(SpeechTabState state)
    {
        _suppressDirtyTracking = true;
        try
        {
            SpeechCheck.IsChecked = state.SpeechEnabled;
            BuildSpeechEngineCombo(state.SpeechEngine);
            BuildVoiceCombo(state.SpeechVoiceId);
            BuildOutputCombo(state.SpeechEngine, state.SpeechOutputId);
            SelectComboValue(SpeechVoiceCombo, state.SpeechVoiceId, _initialVoiceId);
            SelectComboValue(SpeechOutputCombo, state.SpeechOutputId, _initialOutputId);
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdateSpeechControls();
    }

    private bool IsSpeechTabDirty()
    {
        var current = CaptureSpeechTabState();
        var initial = _initialSpeechTabState;
        return current.SpeechEnabled != initial.SpeechEnabled
            || !string.Equals(current.SpeechEngine, initial.SpeechEngine, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.SpeechVoiceId, initial.SpeechVoiceId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.SpeechOutputId, initial.SpeechOutputId, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateSpeechControls()
    {
        var speechEnabled = SpeechCheck.IsChecked == true;
        SpeechEngineCombo.IsEnabled = speechEnabled;
        SpeechVoiceCombo.IsEnabled = speechEnabled;
        if (!speechEnabled)
        {
            SpeechOutputCombo.IsEnabled = false;
            SpeechOutputCombo.ToolTip = "已关闭语音播报。";
            return;
        }

        SpeechOutputCombo.IsEnabled = false;
        SpeechOutputCombo.ToolTip = "当前版本暂不支持播报设备选择。";

        if (SpeechVoiceCombo.Items.Count == 0)
        {
            SpeechVoiceCombo.IsEnabled = false;
        }
    }

    private void BuildSpeechEngineCombo(string? current)
    {
        var items = new[]
        {
            new ComboOption("sapi", "系统语音（SAPI）")
        };
        SpeechEngineCombo.ItemsSource = items;
        SpeechEngineCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechEngineCombo.SelectedValuePath = nameof(ComboOption.Value);
        SpeechEngineCombo.SelectedValue = "sapi";
    }

    private static void BuildSapiVoices(List<ComboOption> voices)
    {
        using var synth = new SpeechSynthesizer();
        foreach (var voice in synth.GetInstalledVoices(CultureInfo.CurrentUICulture))
        {
            var info = voice.VoiceInfo;
            if (!voice.Enabled)
            {
                continue;
            }

            var label = $"{info.Name} ({info.Culture.Name}, {info.Gender})";
            voices.Add(new ComboOption(info.Name, label));
        }

        if (voices.Count == 0)
        {
            voices.Add(new ComboOption(string.Empty, "暂无可用发音人"));
        }
    }

    private void BuildVoiceCombo(string? current)
    {
        var voices = new List<ComboOption>();
        try
        {
            BuildSapiVoices(voices);
        }
        catch (Exception caughtEx) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            voices.Clear();
        }

        if (voices.Count == 0)
        {
            voices.Add(new ComboOption(string.Empty, "暂无可选发音人"));
        }

        SpeechVoiceCombo.ItemsSource = voices;
        SpeechVoiceCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechVoiceCombo.SelectedValuePath = nameof(ComboOption.Value);

        var decision = RollCallVoiceSelectionPolicy.Resolve(
            voices.Select(option => option.Value).ToList(),
            preferredVoiceId: current,
            fallbackVoiceId: _initialVoiceId);

        SpeechVoiceCombo.IsEnabled = decision.IsVoiceSelectionEnabled;
        SpeechVoiceCombo.SelectedValue = decision.SelectedVoiceId;
    }

    private void BuildOutputCombo(string? engine, string? current)
    {
        SpeechOutputCombo.ItemsSource = new[]
        {
            new ComboOption(string.Empty, "当前版本暂不支持输出设备选择")
        };
        SpeechOutputCombo.DisplayMemberPath = nameof(ComboOption.Label);
        SpeechOutputCombo.SelectedValuePath = nameof(ComboOption.Value);
        SpeechOutputCombo.SelectedValue = string.Empty;
        UpdateSpeechControls();
    }
}
