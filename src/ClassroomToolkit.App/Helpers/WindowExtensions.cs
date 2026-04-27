using System;
using System.Windows;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Helpers;

internal static class WindowExtensions
{
    public static bool SafeDragMove(this Window window, Action<Exception>? onFailure = null)
    {
        if (window == null)
        {
            return false;
        }

        try
        {
            using var _ = WindowDragOperationState.Begin();
            window.DragMove();
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            if (onFailure != null)
            {
                try
                {
                    onFailure(ex);
                }
                catch (Exception callbackEx) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(callbackEx))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"WindowExtensions.SafeDragMove failure callback failed: {callbackEx.GetType().Name} - {callbackEx.Message}");
                }
            }
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
