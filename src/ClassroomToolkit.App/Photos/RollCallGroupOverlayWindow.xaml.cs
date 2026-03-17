using System.Windows;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Photos;

public partial class RollCallGroupOverlayWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;
    private bool _isPersistent;

    public RollCallGroupOverlayWindow()
    {
        InitializeComponent();
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _autoCloseTimer.Tick += OnAutoCloseTimerTick;
        Closed += OnOverlayClosed;
        
        Left = 0;
        Top = 0;
    }

    private void OnAutoCloseTimerTick(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        if (!_isPersistent)
        {
            Hide();
        }
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        _autoCloseTimer.Tick -= OnAutoCloseTimerTick;
        Closed -= OnOverlayClosed;
    }

    public void ShowGroup(string groupName, bool persistent = false)
    {
        _isPersistent = persistent;
        GroupNameText.Text = groupName ?? "全部";
        
        Left = 0;
        Top = 0;

        if (persistent)
        {
            // 持久显示模式：不透明，不自动关闭，不播放动画
            _autoCloseTimer.Stop();
            OverlayBorder.Opacity = 1;
            Show();
        }
        else
        {
            // 短暂弹窗模式：播放淡入淡出动画后自动关闭
            _autoCloseTimer.Stop();
            _autoCloseTimer.Start();
            
            if (Resources["FadeStoryboard"] is System.Windows.Media.Animation.Storyboard storyboard)
            {
                storyboard.Begin();
            }
            
            Show();
        }
    }

    public void UpdateGroup(string groupName)
    {
        GroupNameText.Text = groupName ?? "全部";
    }

    public void HideGroup()
    {
        _isPersistent = false;
        _autoCloseTimer.Stop();
        Hide();
    }
}
