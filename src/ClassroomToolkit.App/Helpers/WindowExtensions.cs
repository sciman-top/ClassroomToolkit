using System;
using System.Windows;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Helpers;

public static class WindowExtensions
{
    public static bool SafeDragMove(this Window window, Action<Exception>? onFailure = null)
    {
        if (window == null)
        {
            return false;
        }

        try
        {
            window.DragMove();
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            onFailure?.Invoke(ex);
            return false;
        }
    }

    public static bool? SafeShowDialog(this Window dialog)
    {
        if (dialog == null)
        {
            return false;
        }
        try
        {
            return dialog.ShowDialog();
        }
        catch (InvalidOperationException)
        {
            dialog.Show();
            return dialog.DialogResult;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                $"WindowExtensions.SafeShowDialog failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }
}
