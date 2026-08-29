using System.Windows;

namespace ClassroomToolkit.App.Windowing;

/// <summary>
/// Shows a system message box without allowing the floating-window watchdog to
/// place a Topmost owner above it while its modal loop is active.
/// </summary>
internal static class TopmostMessageBox
{
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
}
