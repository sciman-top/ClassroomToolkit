using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Utilities;

namespace ClassroomToolkit.App;

/// <summary>
/// Launcher UI: minimize/restore, bubble window, auto-exit, diagnostics, drag, about dialog,
/// and window position utilities.
/// </summary>
public partial class MainWindow
{
    private void EnsureFloatingWindowsOnTop()
    {
        ApplyZOrderPolicy();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        MinimizeLauncher(fromSettings: false);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnLauncherSettingsClick(object sender, RoutedEventArgs e)
    {
        var currentMinutes = Math.Max(0, _settings.LauncherAutoExitSeconds / 60);
        var dialog = new AutoExitDialog(currentMinutes)
        {
            Owner = this
        };
        
        // 先修复当前窗口
        try
        {
            BorderFixHelper.FixAllBorders(this);
            System.Diagnostics.Debug.WriteLine("MainWindow: 修复当前窗口完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow 修复失败: {ex.Message}");
        }
        
        // 立即修复新创建的对话框
        try
        {
            BorderFixHelper.FixAllBorders(dialog);
            System.Diagnostics.Debug.WriteLine("MainWindow: 修复对话框完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow 修复对话框失败: {ex.Message}");
        }
        
        // 使用安全显示方法
        bool? result = null;
        try
        {
            result = dialog.SafeShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"对话框显示失败: {ex.Message}");
            throw;
        }
        
        if (result != true)
        {
            return;
        }
        _settings.LauncherAutoExitSeconds = Math.Max(0, dialog.Minutes) * 60;
        ScheduleAutoExitTimer();
        SaveLauncherSettings();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        RequestExit();
    }

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (IsDragBlocked(e.OriginalSource))
        {
            return;
        }
        try
        {
            DragMove();
        }
        catch
        {
            return;
        }
        EnsureWithinWorkArea();
        SaveLauncherSettings();
    }

    private bool IsDragBlocked(object? source)
    {
        return FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(source as DependencyObject) != null;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T match)
            {
                return match;
            }
            obj = GetParent(obj);
        }
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject obj)
    {
        if (obj is System.Windows.Documents.TextElement textElement)
        {
            return textElement.Parent;
        }
        if (obj is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }
        var parent = VisualTreeHelper.GetParent(obj);
        if (parent == null && obj is FrameworkElement element)
        {
            parent = element.Parent as DependencyObject;
        }
        return parent ?? LogicalTreeHelper.GetParent(obj);
    }

    private static string ResolveStudentWorkbookPath()
    {
        return StudentResourceLocator.ResolveStudentWorkbookPath();
    }

    private void ApplyLauncherPosition()
    {
        Left = _settings.LauncherX;
        Top = _settings.LauncherY;
        EnsureWithinWorkArea();
    }

    private void EnsureWithinWorkArea()
    {
        var area = SystemParameters.WorkArea;
        if (Left < area.Left)
        {
            Left = area.Left;
        }
        if (Top < area.Top)
        {
            Top = area.Top;
        }
        if (Left + Width > area.Right)
        {
            Left = area.Right - Width;
        }
        if (Top + Height > area.Bottom)
        {
            Top = area.Bottom - Height;
        }
    }

    private void EnsureBubbleWindow()
    {
        if (_bubbleWindow != null)
        {
            return;
        }
        _bubbleWindow = new LauncherBubbleWindow();
        _bubbleWindow.RestoreRequested += RestoreLauncher;
        _bubbleWindow.PositionChanged += OnBubblePositionChanged;
    }

    private void MinimizeLauncher(bool fromSettings)
    {
        EnsureBubbleWindow();
        if (_bubbleWindow == null)
        {
            return;
        }
        var target = fromSettings
            ? new System.Windows.Point(_settings.LauncherBubbleX, _settings.LauncherBubbleY)
            : new System.Windows.Point(Left + Width / 2, Top + Height / 2);
        _bubbleWindow.PlaceNear(target);
        _bubbleWindow.Show();
        Hide();
        _settings.LauncherMinimized = true;
        SaveLauncherSettings();
    }

    private void RestoreLauncher()
    {
        _settings.LauncherMinimized = false;
        _bubbleWindow?.Hide();
        Show();
        Activate();
        EnsureWithinWorkArea();
        SaveLauncherSettings();
        UpdateToggleButtons();
    }

    private void OnBubblePositionChanged(System.Windows.Point position)
    {
        _settings.LauncherBubbleX = (int)Math.Round(position.X);
        _settings.LauncherBubbleY = (int)Math.Round(position.Y);
        if (_settings.LauncherMinimized)
        {
            SaveLauncherSettings();
        }
    }

    private void ScheduleAutoExitTimer()
    {
        _autoExitTimer.Stop();
        if (_settings.LauncherAutoExitSeconds > 0)
        {
            _autoExitTimer.Interval = TimeSpan.FromSeconds(_settings.LauncherAutoExitSeconds);
            _autoExitTimer.Start();
        }
    }

    private void RunStartupDiagnostics()
    {
        var flag = Environment.GetEnvironmentVariable("CTOOL_NO_STARTUP_DIAG");
        if (string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var settingsPath = _configurationService.SettingsIniPath;
        _ = SafeTaskRunner.Run("MainWindow.StartupDiagnostics", _ =>
        {
            var result = SystemDiagnostics.CollectQuickDiagnostics(settingsPath);
            if (!result.HasIssues)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                if (!IsLoaded)
                {
                    return;
                }
                var dialog = new DiagnosticsDialog(result)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            });
        }, _backgroundTasksCancellation.Token, ex =>
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: startup diagnostics failed: {ex.GetType().Name} - {ex.Message}");
        });
    }

    private void SaveLauncherSettings()
    {
        _settings.LauncherX = (int)Math.Round(Left);
        _settings.LauncherY = (int)Math.Round(Top);
        if (_bubbleWindow != null)
        {
            _settings.LauncherBubbleX = (int)Math.Round(_bubbleWindow.Left);
            _settings.LauncherBubbleY = (int)Math.Round(_bubbleWindow.Top);
        }
        SaveSettings();
    }
}
