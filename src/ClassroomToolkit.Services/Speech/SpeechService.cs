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

                if (_synthesizer == null)
                {
                    _synthesizer = new SpeechSynthesizer();
                    _synthesizer.SpeakCompleted += OnSpeakCompleted;
                }

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

    /// <summary>取消当前会话已排队/播放中的播报（如点名窗口关闭时），不销毁语音引擎。</summary>
    public void CancelSpeaking()
    {
        try
        {
            lock (_syncRoot)
            {
                if (_disposed || _synthesizer == null)
                {
                    return;
                }

                _synthesizer.SpeakAsyncCancelAll();
            }
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            Debug.WriteLine(FormatDiagnostic("CancelSpeaking", ex));
        }
    }

    internal void NotifySpeechUnavailableForTest()
    {
        NotifySpeechUnavailable();
    }

    internal void RaiseSpeakCompletedForTest(Exception? error)
    {
        HandleSpeakCompleted(error);
    }

    private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
    {
        HandleSpeakCompleted(e.Error);
    }

    private void HandleSpeakCompleted(Exception? error)
    {
        if (error == null)
        {
            return;
        }

        // SpeakCompleted 在线程池线程上触发：播报启动后的异步失败（音频设备变更、
        // SAPI 运行时失败）在此才可见，必须接上降级通知，否则播报静默失效。
        bool shouldNotifyUnavailable;
        lock (_syncRoot)
        {
            shouldNotifyUnavailable = SpeechServiceUnavailableNotificationPolicy.ShouldNotify(ref _unavailableNotifiedState);
        }

        Debug.WriteLine(FormatDiagnostic("SpeakCompleted", error));
        if (shouldNotifyUnavailable)
        {
            NotifySpeechUnavailable();
        }
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
