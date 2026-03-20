using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.ViewModels;

/// <summary>
/// ViewModel 基类，提供属性变更通知和通用辅助方法
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
                System.Diagnostics.Debug.WriteLine($"ViewModelBase: PropertyChanged callback failed: {ex.Message}");
            }
        }
    }

    protected void RaisePropertyChanged(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            OnPropertyChanged(name);
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
