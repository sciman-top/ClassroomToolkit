using System;
using System.Windows;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Helpers
{
    /// <summary>
    /// 全局 BorderBrush 修复器，在应用程序启动时立即修复所有控件
    /// </summary>
    public static class GlobalBorderFixer
    {
        /// <summary>
        /// 立即修复应用程序中的所有 Border 控件
        /// </summary>
        public static void FixAllBordersImmediately()
        {
            try
            {
                // 修复主窗口
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    BorderFixHelper.FixAllBorders(mainWindow);
                    System.Diagnostics.Debug.WriteLine("GlobalBorderFixer: 修复主窗口完成");
                }
                
                // 修复所有已打开的窗口
                var currentApp = System.Windows.Application.Current;
                if (currentApp?.Windows != null)
                {
                    foreach (Window window in currentApp.Windows)
                    {
                        if (window != null && window != mainWindow)
                        {
                            BorderFixHelper.FixAllBorders(window);
                            System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer: 修复窗口 {window.GetType().Name} 完成");
                        }
                    }
                }
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer 错误: {ex.Message}");
            }
        }
    }
}
