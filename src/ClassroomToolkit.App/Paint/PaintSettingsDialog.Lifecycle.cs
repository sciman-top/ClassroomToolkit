using System.Windows;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
        if (_sizeToContentCommitted)
        {
            return;
        }
        if (!DispatcherInvokeAvailabilityPolicy.CanBeginInvoke(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            return;
        }

        var scheduled = PaintActionInvoker.TryInvoke(() =>
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    _sizeToContentCommitted = true;
                    SizeToContent = System.Windows.SizeToContent.Manual;
                },
                System.Windows.Threading.DispatcherPriority.ContextIdle);
            return true;
        }, fallback: false);
        if (!scheduled)
        {
            if (Dispatcher.CheckAccess())
            {
                _sizeToContentCommitted = true;
                SizeToContent = System.Windows.SizeToContent.Manual;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PaintSettingsDialog] deferred SizeToContent scheduling skipped");
            }
        }
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        DetachSectionDirtyTrackingHandlers();
        DetachPresetManagedControlHandlers();
        ClassroomWritingModeCombo.SelectionChanged -= OnClassroomWritingModeChanged;
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }
}
