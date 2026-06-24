using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace ClassroomToolkit.Services.Speech;

public class SpeechService : IDisposable
{
    private readonly object _syncRoot = new();
    private SpeechSynthesizer? _synthesizer;
    private string _lastVoiceId = string.Empty;
    private int _unavailableNotifiedState;
    private bool _disposed;

    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based event is part of the existing app contract.")]
    public event Action? SpeechUnavailable;

    public Task SpeakAsync(string text, string? voiceId = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

        Exception? failure = null;
        bool shouldNotifyUnavailable = false;

        try
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                _synthesizer ??= new SpeechSynthesizer();

                if (!string.IsNullOrWhiteSpace(voiceId) && !string.Equals(voiceId, _lastVoiceId, StringComparison.OrdinalIgnoreCase))
                {
                    _synthesizer.SelectVoice(voiceId);
                    _lastVoiceId = voiceId;
                }

                _synthesizer.SpeakAsyncCancelAll();
                _synthesizer.SpeakAsync(text);
                SpeechServiceUnavailableNotificationPolicy.Reset(ref _unavailableNotifiedState);
            }
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            failure = ex;
            shouldNotifyUnavailable = SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref _unavailableNotifiedState);
        }

        if (failure != null)
        {
            Debug.WriteLine(FormatDiagnostic("Speak", failure));
            if (shouldNotifyUnavailable)
            {
                NotifySpeechUnavailable();
            }
        }

        return Task.CompletedTask;
    }

    internal void NotifySpeechUnavailableForTest()
    {
        NotifySpeechUnavailable();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        SpeechSynthesizer? synthesizerToDispose = null;
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
            synthesizerToDispose = _synthesizer;
            _synthesizer = null;
        }

        if (synthesizerToDispose != null)
        {
            try
            {
                synthesizerToDispose.SpeakAsyncCancelAll();
            }
            catch (Exception ex) when (IsNonFatal(ex))
            {
                Debug.WriteLine(FormatDiagnostic("Cancel pending speech", ex));
            }

            try
            {
                synthesizerToDispose.Dispose();
            }
            catch (Exception ex) when (IsNonFatal(ex))
            {
                Debug.WriteLine(FormatDiagnostic("Dispose", ex));
            }
        }
    }

    private void NotifySpeechUnavailable()
    {
        var handlers = SpeechUnavailable?.GetInvocationList();
        if (handlers == null)
        {
            return;
        }

        foreach (var callback in handlers)
        {
            try
            {
                ((Action)callback)();
            }
            catch (Exception callbackEx) when (IsNonFatal(callbackEx))
            {
                Debug.WriteLine(FormatDiagnostic("SpeechUnavailable callback", callbackEx));
            }
        }
    }

    private static string FormatDiagnostic(string operation, Exception ex)
    {
        return $"[SpeechService] {operation} failed: {ex.GetType().Name} - {ex.Message}";
    }

    private static bool IsNonFatal(Exception ex)
    {
        return ex is not (
            OutOfMemoryException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or StackOverflowException
            or AccessViolationException);
    }
}
