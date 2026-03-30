using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Helpers
{
    /// <summary>
    /// 全局 Border 修复工具，自动修复 BorderBrush 问题
    /// </summary>
    public static class BorderFixHelper
    {
        /// <summary>
        /// 修复窗口中所有有问题的 Border 控件
        /// </summary>
        public static void FixAllBorders(Window window)
        {
            try
            {
                FixBordersRecursive(window);
                System.Diagnostics.Debug.WriteLine("BorderFixHelper: 所有 Border 控件已检查并修复");
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine($"BorderFixHelper 错误: {ex.Message}");
            }
        }

        private static void FixBordersRecursive(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Border border)
                {
                    FixBorderIfNeeded(border);
                }
                
                // 递归检查子元素
                FixBordersRecursive(child);
            }
        }

        private static void FixBorderIfNeeded(Border border)
        {
            var cornerRadius = border.CornerRadius;
            
            // 如果有圆角但没有边框，自动修复
            if (cornerRadius != new CornerRadius(0))
            {
                var borderBrush = border.BorderBrush;
                
                // 检查是否为 DependencyProperty.UnsetValue 或 null
                if (borderBrush == null || borderBrush == DependencyProperty.UnsetValue)
                {
                    try
                    {
                        // 设置透明边框
                        border.BorderBrush = System.Windows.Media.Brushes.Transparent;
                        
                        // 记录修复
                        var name = border.Name ?? "(未命名)";
                        var parentName = (border.Parent as FrameworkElement)?.Name ?? "(未知父元素)";
                        System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 修复 Border '{name}' (父元素: {parentName})");
                    }
                    catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                    {
                        System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 修复 Border 失败 - {ex.Message}");
                        
                        // 尝试其他方法：清除并重新设置
                        try
                        {
                            border.ClearValue(Border.BorderBrushProperty);
                            border.BorderBrush = System.Windows.Media.Brushes.Transparent;
                            System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 使用 ClearValue 方法修复 Border '{border.Name}'");
                        }
                        catch (Exception ex2) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex2))
                        {
                            System.Diagnostics.Debug.WriteLine($"BorderFixHelper: ClearValue 方法也失败 - {ex2.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 为所有窗口注册全局修复
        /// </summary>
        public static void RegisterGlobalFix()
        {
            // 监听窗口 Loaded 事件
            EventManager.RegisterClassHandler(
                typeof(Window),
                Window.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
            
            // 立即修复当前已存在的窗口
            try
            {
                var currentWindow = System.Windows.Application.Current?.MainWindow;
                if (currentWindow != null)
                {
                    FixAllBorders(currentWindow);
                    System.Diagnostics.Debug.WriteLine("BorderFixHelper: 修复主窗口完成");
                }
            }
            catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 修复主窗口失败: {ex.Message}");
            }
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                // 立即修复，不等待布局完成
                try
                {
                    FixAllBorders(window);
                    System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 窗口 {window.GetType().Name} 加载时修复完成");
                }
                catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    System.Diagnostics.Debug.WriteLine($"BorderFixHelper 加载修复失败: {ex.Message}");
                }
                
                // 延迟再次执行，确保动态创建的控件也被修复
                try
                {
                    if (window.Dispatcher.HasShutdownStarted || window.Dispatcher.HasShutdownFinished)
                    {
                        return;
                    }

                    void ApplyDeferredBorderFix()
                    {
                        try
                        {
                            FixAllBorders(window);
                            System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 窗口 {window.GetType().Name} 延迟修复完成");
                        }
                        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                        {
                            System.Diagnostics.Debug.WriteLine($"BorderFixHelper 延迟修复失败: {ex.Message}");
                        }
                    }

                    var scheduled = false;
                    window.Dispatcher.BeginInvoke(
                        new Action(ApplyDeferredBorderFix),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                    scheduled = true;
                    if (!scheduled && window.Dispatcher.CheckAccess())
                    {
                        ApplyDeferredBorderFix();
                    }
                }
                catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    System.Diagnostics.Debug.WriteLine($"BorderFixHelper 延迟调度失败: {ex.Message}");
                    if (window.Dispatcher.CheckAccess())
                    {
                        try
                        {
                            FixAllBorders(window);
                        }
                        catch (Exception fallbackEx) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(fallbackEx))
                        {
                            System.Diagnostics.Debug.WriteLine($"BorderFixHelper 延迟回退修复失败: {fallbackEx.Message}");
                        }
                    }
                }
            }
        }
    }
}
