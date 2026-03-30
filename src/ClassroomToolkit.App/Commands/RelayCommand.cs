using System.Windows.Input;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Commands;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute();
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        var handlers = CanExecuteChanged?.GetInvocationList();
        if (handlers == null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler)handler)(this, EventArgs.Empty);
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine($"RelayCommand: CanExecuteChanged callback failed: {ex.Message}");
            }
        }
    }
}
