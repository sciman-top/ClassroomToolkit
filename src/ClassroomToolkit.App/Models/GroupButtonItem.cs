using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Models;

public sealed class GroupButtonItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public GroupButtonItem(string label, bool isReset)
    {
        Label = label ?? string.Empty;
        IsReset = isReset;
    }

    public string Label { get; }

    public bool IsReset { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var handlers = PropertyChanged?.GetInvocationList();
        if (handlers == null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((PropertyChangedEventHandler)handler)(this, new PropertyChangedEventArgs(propertyName));
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine($"GroupButtonItem: PropertyChanged callback failed: {ex.Message}");
            }
        }
    }
}
