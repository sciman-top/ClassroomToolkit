using System;
using System.Windows;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Helpers;

public static class WindowExtensions
{
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
