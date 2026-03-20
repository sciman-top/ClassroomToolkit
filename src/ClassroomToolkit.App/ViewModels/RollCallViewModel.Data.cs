using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Services;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.ViewModels;

public sealed partial class RollCallViewModel
{
    public void WarmupData(string path)
    {
        if (_disposed || _disposeCancellation.IsCancellationRequested) return;
        _loadOrchestrator.Warmup(path, _disposeCancellation.Token);
    }

    public void LoadData(string? preferredClass = null)
    {
        var result = _loadOrchestrator.Load(_dataPath);
        ApplyLoadResult(result, preferredClass);
    }

    public async Task LoadDataAsync(string? preferredClass, Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (_disposed || _disposeCancellation.IsCancellationRequested)
        {
            return;
        }

        var preload = _loadOrchestrator.TryConsumePreloaded(_dataPath);
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

        RollCallWorkbookLoadResult result;
        try
        {
            result = await Task.Run(() => _loadOrchestrator.Load(_dataPath), _disposeCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
            return;
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

    private void ApplyLoadResult(RollCallWorkbookLoadResult result, string? preferredClass)
    {
        if (_disposed || _disposeCancellation.IsCancellationRequested)
        {
            return;
        }

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
            SafeActionExecutionExecutor.TryExecute(
                () => DataLoadFailed?.Invoke(result.ErrorMessage),
                ex => System.Diagnostics.Debug.WriteLine($"RollCallViewModel: data load failed callback failed: {ex.Message}"));
        }
    }
}
