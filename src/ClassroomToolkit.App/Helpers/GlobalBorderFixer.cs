using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    FixAllBordersRecursive(mainWindow);
                    System.Diagnostics.Debug.WriteLine("GlobalBorderFixer: 修复主窗口完成");
                }
                
                // 修复所有已打开的窗口
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != null && window != mainWindow)
                    {
                        FixAllBordersRecursive(window);
                        System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer: 修复窗口 {window.GetType().Name} 完成");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 递归修复窗口中的所有 Border 控件
        /// </summary>
        private static void FixAllBordersRecursive(DependencyObject parent)
        {
            try
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    // 如果是 Border，强制修复
                    if (child is Border border)
                    {
                        ForceFixBorder(border);
                    }
                    
                    // 递归处理子控件
                    FixAllBordersRecursive(child);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FixAllBordersRecursive 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 强制修复 Border 控件
        /// </summary>
        private static void ForceFixBorder(Border border)
        {
            try
            {
                var cornerRadius = border.CornerRadius;
                
                if (cornerRadius != new CornerRadius(0))
                {
                    var borderBrush = border.BorderBrush;
                    
                    // 检查是否为 DependencyProperty.UnsetValue 或 null
                    if (borderBrush == null || borderBrush == DependencyProperty.UnsetValue)
                    {
                        try
                        {
                            // 设置透明边框
                            border.BorderBrush = Brushes.Transparent;
                            
                            var name = border.Name ?? "(未命名)";
                            var parentName = (border.Parent as FrameworkElement)?.Name ?? "(未知父元素)";
                            System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer: 修复 Border '{name}' (父元素: {parentName})");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer: 修复 Border 失败 - {ex.Message}");
                            
                            // 尝试其他方法：清除并重新设置
                            try
                            {
                                border.ClearValue(Border.BorderBrushProperty);
                                border.BorderBrush = Brushes.Transparent;
                                System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer: 使用 ClearValue 方法修复 Border '{border.Name}'");
                            }
                            catch (Exception ex2)
                            {
                                System.Diagnostics.Debug.WriteLine($"GlobalBorderFixer: ClearValue 方法也失败 - {ex2.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceFixBorder 错误: {ex.Message}");
            }
        }
    }
}
