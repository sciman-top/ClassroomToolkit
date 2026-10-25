using System.Runtime.InteropServices;
using System.Windows;

namespace ClassroomToolkit.App.Windowing;

/// <summary>
/// Shows a system message box without allowing the floating-window watchdog to
/// place a Topmost owner above it while its modal loop is active.
/// </summary>
internal static class TopmostMessageBox
{
    private const uint MbOk = 0x0u;
    private const uint MbIconError = 0x10u;
    private const uint MbIconInformation = 0x40u;
    private const uint MbTopmost = 0x40000u;

    internal static MessageBoxResult Show(
        Window owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        return ExecuteWithOwnerTopmostSuppressed(
            owner,
            () => System.Windows.MessageBox.Show(owner, messageBoxText, caption, button, icon));
    }

    internal static MessageBoxResult Show(
        Window owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult)
    {
        return ExecuteWithOwnerTopmostSuppressed(
            owner,
            () => System.Windows.MessageBox.Show(owner, messageBoxText, caption, button, icon, defaultResult));
    }

    /// <summary>
    /// Owner-less variant for early startup (before any Window exists) or fatal
    /// paths where no owner can be resolved; MB_TOPMOST keeps the notice above
    /// fullscreen presentation windows and our own Topmost floating windows.
    /// </summary>
    internal static void ShowNativeTopmost(string messageBoxText, string caption, bool isError)
    {
        try
        {
            var icon = isError ? MbIconError : MbIconInformation;
            _ = MessageBox(IntPtr.Zero, messageBoxText, caption, MbOk | icon | MbTopmost);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                $"TopmostMessageBox.ShowNativeTopmost failed: {ex.GetType().Name} - {ex.Message}");
        }
    }

    internal static TResult ExecuteWithOwnerTopmostSuppressed<TResult>(Window owner, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(action);

        var restoreOwnerTopmost = owner.Topmost;
        var loweredOwnerTopmost = false;
        using var suppressionScope = FloatingTopmostDialogSuppressionState.Enter();
        try
        {
            if (restoreOwnerTopmost)
            {
                owner.Topmost = false;
                loweredOwnerTopmost = true;
            }

            return action();
        }
        finally
        {
            if (loweredOwnerTopmost)
            {
                _ = SafeActionExecutionExecutor.TryExecute(
                    () =>
                    {
                        owner.Topmost = restoreOwnerTopmost;
                        WindowTopmostExecutor.ApplyNoActivate(owner, enabled: restoreOwnerTopmost, enforceZOrder: true);
                    });
            }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
