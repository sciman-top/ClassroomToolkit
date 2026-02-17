using System;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace ClassroomToolkit.Services.Speech;

public class SpeechService : IDisposable
{
    private SpeechSynthesizer? _synthesizer;
    private string _lastVoiceId = string.Empty;
    private bool _unavailableNotified;
    private bool _disposed;

    public event Action? SpeechUnavailable;

    public async Task SpeakAsync(string text, string? voiceId = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            if (_synthesizer == null)
            {
                _synthesizer = await Task.Run(() => new SpeechSynthesizer());
            }

            if (!string.IsNullOrWhiteSpace(voiceId) && !string.Equals(voiceId, _lastVoiceId, StringComparison.OrdinalIgnoreCase))
            {
                _synthesizer.SelectVoice(voiceId);
                _lastVoiceId = voiceId;
            }

            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpeechService] Speak failed: {ex.Message}");
            if (!_unavailableNotified)
            {
                _unavailableNotified = true;
                SpeechUnavailable?.Invoke();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _synthesizer?.Dispose();
        _synthesizer = null;
        GC.SuppressFinalize(this);
    }
}
