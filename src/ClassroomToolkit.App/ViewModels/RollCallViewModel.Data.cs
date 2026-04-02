using System;
using System.IO;
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

    public void WarmupData(string path)
    {
        if (_disposed || _disposeCancellation.IsCancellationRequested) return;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) return;

        if (!TryGetFileWriteTimeUtc(path, out var writeTimeUtc))
        {
            return;
        }

        lock (_preloadLock)
        {
            if (_preloadedResult != null && (!string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) || _preloadedWriteTimeUtc != writeTimeUtc))
            {
                _preloadedResult = null;
            }
            if (_preloadedResult != null && string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) && _preloadedWriteTimeUtc == writeTimeUtc)
            {
                return;
            }
            if (_preloadTask != null && string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) && _preloadedWriteTimeUtc == writeTimeUtc)
            {
                return;
            }

            _preloadedPath = path;
            _preloadedWriteTimeUtc = writeTimeUtc;
            var expectedPath = path;
            var expectedWriteTimeUtc = writeTimeUtc;
            var preloadTask = Task.Run(() => LoadDataFromPath(expectedPath), _disposeCancellation.Token);
            _preloadTask = preloadTask;
            preloadTask.ContinueWith(task =>
            {
                lock (_preloadLock)
                {
                    if (_disposed || _disposeCancellation.IsCancellationRequested)
                    {
                        if (ReferenceEquals(_preloadTask, preloadTask))
                        {
                            _preloadTask = null;
                        }
                        return;
                    }
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        var completedResult = task.GetAwaiter().GetResult();
                        if (string.Equals(_preloadedPath, expectedPath, StringComparison.OrdinalIgnoreCase) && _preloadedWriteTimeUtc == expectedWriteTimeUtc)
                        {
                            if (string.IsNullOrWhiteSpace(completedResult.ErrorMessage))
                            {
                                _preloadedResult = completedResult;
                            }
                        }
                    }
                    if (ReferenceEquals(_preloadTask, preloadTask))
                    {
                        _preloadTask = null;
                    }
                }
            }, TaskScheduler.Default);
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

        if (!TryGetFileWriteTimeUtc(path, out var writeTimeUtc))
        {
            return null;
        }

        lock (_preloadLock)
        {
            if (!string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase)
                || _preloadedWriteTimeUtc != writeTimeUtc)
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

        if (!TryGetFileWriteTimeUtc(path, out var writeTimeUtc))
        {
            return null;
        }

        Task<RollCallLoadResult>? preloadTask = null;
        bool consumeTask = false;

        lock (_preloadLock)
        {
            if (!string.Equals(_preloadedPath, path, StringComparison.OrdinalIgnoreCase) || _preloadedWriteTimeUtc != writeTimeUtc)
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
                if (!preloadTask.IsCompletedSuccessfully) return null;
                var result = preloadTask.GetAwaiter().GetResult();
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
        return new RollCallLoadResult(result.Workbook, result.ClassStates, result.ErrorMessage);
    }

    private static bool TryGetFileWriteTimeUtc(string path, out DateTime writeTimeUtc)
    {
        writeTimeUtc = default;
        try
        {
            writeTimeUtc = File.GetLastWriteTimeUtc(path);
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
        _canPersistWorkbook = string.IsNullOrWhiteSpace(result.ErrorMessage);
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
    }

    private sealed record RollCallLoadResult(StudentWorkbook Workbook, Dictionary<string, ClassRollState> ClassStates, string? ErrorMessage);
}
