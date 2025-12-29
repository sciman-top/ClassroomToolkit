using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            catch (Exception ex)
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
                
                if (borderBrush == null || borderBrush == DependencyProperty.UnsetValue)
                {
                    // 设置透明边框
                    border.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    
                    // 记录修复
                    var name = border.Name ?? "(未命名)";
                    var parentName = (border.Parent as FrameworkElement)?.Name ?? "(未知父元素)";
                    System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 修复 Border '{name}' (父元素: {parentName})");
                }
            }
        }

        /// <summary>
        /// 为所有窗口注册全局修复
        /// </summary>
        public static void RegisterGlobalFix()
        {
            // 监听窗口 SourceInitialized 事件，比 Loaded 更早
            EventManager.RegisterClassHandler(
                typeof(Window),
                Window.SourceInitializedEvent,
                new EventHandler(OnWindowSourceInitialized));
            
            // 同时监听 Loaded 事件，确保动态创建的控件也被处理
            EventManager.RegisterClassHandler(
                typeof(Window),
                Window.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
        }

        private static void OnWindowSourceInitialized(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                try
                {
                    // 在窗口源初始化时立即修复
                    FixAllBorders(window);
                    System.Diagnostics.Debug.WriteLine($"BorderFixHelper: 窗口 {window.GetType().Name} 源初始化时修复完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"BorderFixHelper 源初始化修复失败: {ex.Message}");
                }
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"BorderFixHelper 立即修复失败: {ex.Message}");
                }
                
                // 延迟再次执行，确保动态创建的控件也被修复
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        FixAllBorders(window);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"BorderFixHelper 延迟修复失败: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}
