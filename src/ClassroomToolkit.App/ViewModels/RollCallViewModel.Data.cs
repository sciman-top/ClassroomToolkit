using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using ClassroomToolkit.Domain.Services;
using ClassroomToolkit.Infra.Storage;

namespace ClassroomToolkit.App.ViewModels;

public sealed partial class RollCallViewModel
{
    private static readonly object PreloadLock = new();
    private static Task<RollCallLoadResult>? _preloadTask;
    private static RollCallLoadResult? _preloadedResult;
    private static string? _preloadedPath;
    private static DateTime _preloadedWriteTimeUtc;

    public static void WarmupData(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) return;

        DateTime writeTimeUtc;
        try { writeTimeUtc = File.GetLastWriteTimeUtc(path); }
        catch { return; }

        lock (PreloadLock)
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
                lock (PreloadLock)
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

    private static RollCallLoadResult? TryConsumePreloadedResult(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        DateTime writeTimeUtc;
        try { writeTimeUtc = File.GetLastWriteTimeUtc(path); }
        catch { return null; }

        Task<RollCallLoadResult>? preloadTask = null;
        bool consumeTask = false;

        lock (PreloadLock)
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
            catch { return null; }
        }

        return null;
    }

    private static RollCallLoadResult LoadDataFromPath(string path)
    {
        try
        {
            var store = new StudentWorkbookStore();
            var result = store.LoadOrCreate(path);
            var states = new Dictionary<string, ClassRollState>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in RollStateSerializer.DeserializeWorkbookStates(result.RollStateJson))
            {
                states[pair.Key] = pair.Value;
            }
            return new RollCallLoadResult(result.Workbook, states, null);
        }
        catch (Exception ex)
        {
            var fallbackRoster = new ClassRoster("班级1", Array.Empty<StudentRecord>());
            var fallbackWorkbook = new StudentWorkbook(new Dictionary<string, ClassRoster> { ["班级1"] = fallbackRoster }, "班级1");
            return new RollCallLoadResult(fallbackWorkbook, new Dictionary<string, ClassRollState>(), $"学生名册读取失败：{ex.Message}");
        }
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
