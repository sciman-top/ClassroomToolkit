using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ClassroomToolkit.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private bool _isPaintActive;
    private bool _isRollCallVisible;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand? OpenRollCallSettingsCommand { get; set; }

    public ICommand? OpenPaintSettingsCommand { get; set; }

    public bool IsPaintActive
    {
        get => _isPaintActive;
        set
        {
            if (_isPaintActive == value)
            {
                return;
            }

            _isPaintActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PaintButtonText));
        }
    }

    public bool IsRollCallVisible
    {
        get => _isRollCallVisible;
        set
        {
            if (_isRollCallVisible == value)
            {
                return;
            }

            _isRollCallVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RollCallButtonText));
        }
    }

    public string PaintButtonText => IsPaintActive ? "隐藏画笔" : "画笔";

    public string RollCallButtonText => IsRollCallVisible ? "隐藏点名" : "点名";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
