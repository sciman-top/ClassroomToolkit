using System;
using System.Windows;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Windowing;

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
        catch (Exception ex) when (AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            if (onFailure != null)
            {
                try
                {
                    onFailure(ex);
                }
                catch (Exception callbackEx) when (AppGlobalExceptionHandling.IsNonFatal(callbackEx))
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
        catch (InvalidOperationException) when (dialog.IsVisible)
        {
            // 已在别处可见：前置激活而不是再弹一份；按取消处理。
            try
            {
                dialog.Activate();
            }
            catch (Exception activateEx) when (AppGlobalExceptionHandling.IsNonFatal(activateEx))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"WindowExtensions.SafeShowDialog activate failed: {activateEx.GetType().Name} - {activateEx.Message}");
            }

            return null;
        }
        catch (InvalidOperationException ex)
        {
            // 降级非模态 Show 会让设置 DialogResult 的按钮二次抛异常，产出半坏的
            // 对话框；与通用异常路径一致按取消处理并留痕。
            System.Diagnostics.Debug.WriteLine(
                $"WindowExtensions.SafeShowDialog failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
        catch (Exception ex) when (AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                $"WindowExtensions.SafeShowDialog failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }
}
