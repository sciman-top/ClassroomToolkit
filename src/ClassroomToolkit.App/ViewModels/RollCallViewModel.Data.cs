using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClassroomToolkit.App;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Services;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.ViewModels;

public sealed partial class RollCallViewModel
{
    private readonly object _preloadLock = new();
    private Task<RollCallLoadResult>? _preloadTask;
    private RollCallLoadResult? _preloadedResult;
    private string? _preloadedPath;
    private DateTime _preloadedWriteTimeUtc;
    private long _preloadedLength;
    private string? _preloadedContentHash;

    public void WarmupData(string path)
    {
        if (_disposed || _disposeCancellation.IsCancellationRequested) return;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) return;

        if (!TryGetFileFingerprint(path, out var fingerprint))
        {
            return;
        }

        lock (_preloadLock)
        {
            if (_preloadedResult != null && !MatchesPreloadFingerprint(path, fingerprint))
            {
                _preloadedResult = null;
            }
            if (_preloadedResult != null && MatchesPreloadFingerprint(path, fingerprint))
            {
                return;
            }
            if (_preloadTask != null && MatchesPreloadFingerprint(path, fingerprint))
            {
                return;
            }

            _preloadedPath = path;
            _preloadedWriteTimeUtc = fingerprint.WriteTimeUtc;
            _preloadedLength = fingerprint.Length;
            _preloadedContentHash = fingerprint.ContentHash;
            var expectedPath = path;
            var expectedFingerprint = fingerprint;
            var preloadTask = Task.Run(() => LoadDataFromPath(expectedPath), _disposeCancellation.Token);
            _preloadTask = preloadTask;
            _ = preloadTask.ContinueWith(
                task => CompletePreloadTask(task, expectedPath, expectedFingerprint),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    public void LoadData(string? preferredClass = null)
    {
        var result = LoadDataCore();
        ApplyLoadResult(result, preferredClass);
    }

    public async Task LoadDataAsync(string? preferredClass, Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (_disposed || _disposeCancellation.IsCancellationRequested)
        {
            return;
        }

        var preload = TryConsumePreloadedResult(_dataPath);
        if (preload != null)
        {
            if (_disposed || _disposeCancellation.IsCancellationRequested)
            {
                return;
            }
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return;
            }
            await dispatcher.InvokeAsync(() =>
            {
                ApplyLoadResult(preload, preferredClass);
            }, DispatcherPriority.Render);
            return;
        }

        RollCallLoadResult result;
        var pendingPreloadTask = TryGetMatchingPreloadTask(_dataPath);
        if (pendingPreloadTask != null)
        {
            try
            {
                result = await pendingPreloadTask.WaitAsync(_disposeCancellation.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    result = await Task.Run(LoadDataCore, _disposeCancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine(
                    RollCallDataLoadDiagnosticsPolicy.FormatPreloadConsumeFailure(
                        _dataPath,
                        ex.GetType().Name,
                        ex.Message));
                result = await Task.Run(LoadDataCore, _disposeCancellation.Token).ConfigureAwait(false);
            }
        }
        else
        {
            try
            {
                result = await Task.Run(LoadDataCore, _disposeCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
            {
                return;
            }
        }
        if (_disposed || _disposeCancellation.IsCancellationRequested)
        {
            return;
        }
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }
        await dispatcher.InvokeAsync(() =>
        {
            ApplyLoadResult(result, preferredClass);
        }, DispatcherPriority.Render);
    }

    private Task<RollCallLoadResult>? TryGetMatchingPreloadTask(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (!TryGetFileFingerprint(path, out var fingerprint))
        {
            return null;
        }

        lock (_preloadLock)
        {
            if (!MatchesPreloadFingerprint(path, fingerprint))
            {
                return null;
            }

            return _preloadTask;
        }
    }

    private RollCallLoadResult LoadDataCore()
    {
        return TryConsumePreloadedResult(_dataPath) ?? LoadDataFromPath(_dataPath);
    }

    private RollCallLoadResult? TryConsumePreloadedResult(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        if (!TryGetFileFingerprint(path, out var fingerprint))
        {
            return null;
        }

        Task<RollCallLoadResult>? preloadTask = null;
        bool consumeTask = false;

        lock (_preloadLock)
        {
            if (!MatchesPreloadFingerprint(path, fingerprint))
            {
                _preloadedResult = null;
                return null;
            }
            if (_preloadedResult != null)
            {
                var result = _preloadedResult;
                _preloadedResult = null;
                return result;
            }
            if (_preloadTask != null && _preloadTask.IsCompleted)
            {
                preloadTask = _preloadTask;
                _preloadTask = null;
                consumeTask = true;
            }
        }

        if (preloadTask != null && consumeTask)
        {
            try
            {
                if (!TryReadCompletedSuccessfulPreloadResult(preloadTask, out var result)) return null;
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) return null;
                return result;
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine(
                    RollCallDataLoadDiagnosticsPolicy.FormatPreloadConsumeFailure(
                        path,
                        ex.GetType().Name,
                        ex.Message));
                return null;
            }
        }

        return null;
    }

    private RollCallLoadResult LoadDataFromPath(string path)
    {
        var result = _workbookUseCase.Load(path);
        return new RollCallLoadResult(result.Workbook, result.ClassStates, result.ErrorMessage, result.OverwriteBlocked);
    }

    private void CompletePreloadTask(
        Task<RollCallLoadResult> preloadTask,
        string expectedPath,
        FileFingerprint expectedFingerprint)
    {
        lock (_preloadLock)
        {
            try
            {
                if (preloadTask.IsCanceled)
                {
                    System.Diagnostics.Debug.WriteLine(
                        RollCallDataLoadDiagnosticsPolicy.FormatPreloadTaskCanceled(expectedPath));
                    return;
                }

                if (preloadTask.IsFaulted)
                {
                    var failure = preloadTask.Exception?.GetBaseException();
                    if (failure != null && AppGlobalExceptionHandlingPolicy.IsNonFatal(failure))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            RollCallDataLoadDiagnosticsPolicy.FormatPreloadTaskFaulted(
                                expectedPath,
                                failure.GetType().Name,
                                failure.Message));
                        return;
                    }

                    if (failure != null)
                    {
                        ExceptionDispatchInfo.Capture(failure).Throw();
                    }
                    return;
                }

                if (_disposed || _disposeCancellation.IsCancellationRequested)
                {
                    return;
                }

                if (!TryReadCompletedSuccessfulPreloadResult(preloadTask, out var completedResult))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(completedResult.ErrorMessage))
                {
                    return;
                }

                if (MatchesPreloadFingerprint(expectedPath, expectedFingerprint))
                {
                    _preloadedResult = completedResult;
                }
            }
            finally
            {
                if (ReferenceEquals(_preloadTask, preloadTask))
                {
                    _preloadTask = null;
                }
            }
        }
    }

    private static bool TryReadCompletedSuccessfulPreloadResult(
        Task<RollCallLoadResult> preloadTask,
        out RollCallLoadResult result)
    {
        result = default!;
        if (!preloadTask.IsCompletedSuccessfully)
        {
            return false;
        }

        var awaiter = preloadTask.GetAwaiter();
        result = awaiter.GetResult();
        return true;
    }

    private bool MatchesPreloadFingerprint(string path, FileFingerprint fingerprint)
    {
        return string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase)
            && _preloadedWriteTimeUtc == fingerprint.WriteTimeUtc
            && _preloadedLength == fingerprint.Length
            && string.Equals(_preloadedContentHash, fingerprint.ContentHash, StringComparison.Ordinal);
    }

    private static bool TryGetFileFingerprint(string path, out FileFingerprint fingerprint)
    {
        fingerprint = default;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return false;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var contentHash = Convert.ToHexString(SHA256.HashData(stream));
            fingerprint = new FileFingerprint(info.Length, info.LastWriteTimeUtc, contentHash);
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                RollCallDataLoadDiagnosticsPolicy.FormatFileWriteTimeReadFailure(
                    path,
                    ex.GetType().Name,
                    ex.Message));
            return false;
        }
    }

    private void ApplyLoadResult(RollCallLoadResult result, string? preferredClass)
    {
        if (_disposed || _disposeCancellation.IsCancellationRequested)
        {
            return;
        }

        _workbook = result.Workbook;
        _isDataReady = true;
        _canPersistWorkbook = string.IsNullOrWhiteSpace(result.ErrorMessage) && !result.OverwriteBlocked;
        _classStates.Clear();
        foreach (var pair in result.ClassStates)
        {
            _classStates[pair.Key] = pair.Value;
        }

        if (!string.IsNullOrWhiteSpace(preferredClass) &&
            _workbook.ClassNames.Any(name => name.Equals(preferredClass.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            _workbook.SetActiveClass(preferredClass.Trim());
        }

        _engine = new RollCallEngine(_workbook.GetActiveRoster());
        ActiveClassName = _workbook.ActiveClass;
        AvailableClasses = _workbook.ClassNames;
        RaisePropertyChanged(nameof(HasStudentData));

        if (_classStates.TryGetValue(_workbook.ActiveClass, out var state))
        {
            _engine.RestoreState(state);
        }

        RefreshGroups();
        CurrentGroup = _engine.CurrentGroup;
        UpdateCurrentStudent();
        _photoResolver.WarmupCache(_workbook.ClassNames);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => DataLoadFailed?.Invoke(result.ErrorMessage),
                ex => System.Diagnostics.Debug.WriteLine($"RollCallViewModel: data load failed callback failed: {ex.Message}"));
        }
        else if (result.OverwriteBlocked)
        {
            SafeActionExecutionExecutor.TryExecute(
                () => DataLoadFailed?.Invoke("学生名册已进入只读模式（规范化备份写入失败，文件可能被占用或目录只读）：本节课点名结果不会写入文件。"),
                ex => System.Diagnostics.Debug.WriteLine($"RollCallViewModel: read-only notice callback failed: {ex.Message}"));
        }
    }

    private readonly record struct FileFingerprint(long Length, DateTime WriteTimeUtc, string ContentHash);

    private sealed record RollCallLoadResult(StudentWorkbook Workbook, Dictionary<string, ClassRollState> ClassStates, string? ErrorMessage, bool OverwriteBlocked = false);
}
