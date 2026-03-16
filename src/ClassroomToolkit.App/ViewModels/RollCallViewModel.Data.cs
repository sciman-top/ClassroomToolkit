using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClassroomToolkit.App;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Services;

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
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) return;

        DateTime writeTimeUtc;
        try { writeTimeUtc = File.GetLastWriteTimeUtc(path); }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                RollCallDataLoadDiagnosticsPolicy.FormatFileWriteTimeReadFailure(
                    path,
                    ex.GetType().Name,
                    ex.Message));
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
            _preloadTask = Task.Run(() => LoadDataFromPath(expectedPath));
            _preloadTask.ContinueWith(task =>
            {
                lock (_preloadLock)
                {
                    if (task.Status == TaskStatus.RanToCompletion && string.IsNullOrWhiteSpace(task.Result.ErrorMessage))
                    {
                        if (string.Equals(_preloadedPath, expectedPath, StringComparison.OrdinalIgnoreCase) && _preloadedWriteTimeUtc == expectedWriteTimeUtc)
                        {
                            _preloadedResult = task.Result;
                        }
                    }
                    _preloadTask = null;
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
        var preload = TryConsumePreloadedResult(_dataPath);
        if (preload != null)
        {
            await dispatcher.InvokeAsync(() =>
            {
                ApplyLoadResult(preload, preferredClass);
            }, DispatcherPriority.Render);
            return;
        }

        var result = await Task.Run(LoadDataCore).ConfigureAwait(false);
        await dispatcher.InvokeAsync(() =>
        {
            ApplyLoadResult(result, preferredClass);
        }, DispatcherPriority.Render);
    }

    private RollCallLoadResult LoadDataCore()
    {
        return TryConsumePreloadedResult(_dataPath) ?? LoadDataFromPath(_dataPath);
    }

    private RollCallLoadResult? TryConsumePreloadedResult(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        DateTime writeTimeUtc;
        try { writeTimeUtc = File.GetLastWriteTimeUtc(path); }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                RollCallDataLoadDiagnosticsPolicy.FormatFileWriteTimeReadFailure(
                    path,
                    ex.GetType().Name,
                    ex.Message));
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
                var result = preloadTask.Result;
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

    private void ApplyLoadResult(RollCallLoadResult result, string? preferredClass)
    {
        _workbook = result.Workbook;
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
            DataLoadFailed?.Invoke(result.ErrorMessage);
        }
    }

    private sealed record RollCallLoadResult(StudentWorkbook Workbook, Dictionary<string, ClassRollState> ClassStates, string? ErrorMessage);
}
