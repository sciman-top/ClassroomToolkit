using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ClassroomToolkit.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private bool _isPaintActive;
    private bool _isRollCallVisible;

    public ICommand? OpenRollCallSettingsCommand { get; set; }

    public ICommand? OpenPaintSettingsCommand { get; set; }

    public bool IsPaintActive
    {
        get => _isPaintActive;
        set
        {
            if (SetField(ref _isPaintActive, value))
            {
                OnPropertyChanged(nameof(PaintButtonText));
            }
        }
    }

    public bool IsRollCallVisible
    {
        get => _isRollCallVisible;
        set
        {
            if (SetField(ref _isRollCallVisible, value))
            {
                OnPropertyChanged(nameof(RollCallButtonText));
            }
        }
    }

    public string PaintButtonText => IsPaintActive ? "隐藏画笔" : "画笔";

    public string RollCallButtonText => IsRollCallVisible ? "隐藏点名" : "点名";
}

