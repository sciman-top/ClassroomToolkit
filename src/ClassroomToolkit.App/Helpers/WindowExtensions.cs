using System;
using System.Windows;

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
    }
}
