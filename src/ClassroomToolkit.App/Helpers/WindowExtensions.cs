using System;
using System.Windows;

namespace ClassroomToolkit.App.Helpers
{
    /// <summary>
    /// 窗口扩展方法，提供安全的显示功能
    /// </summary>
    public static class WindowExtensions
    {
        /// <summary>
        /// 安全地显示对话框，自动修复 BorderBrush 问题
        /// </summary>
        public static bool? SafeShowDialog(this Window window)
        {
            try
            {
                // 在显示前强制修复所有控件
                ForceFixAllControls(window);
                
                // 显示对话框
                return window.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeShowDialog 失败: {ex.Message}");
                
                // 尝试修复后再次显示
                try
                {
                    ForceFixAllControls(window);
                    return window.ShowDialog();
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"SafeShowDialog 第二次尝试也失败: {ex2.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// 强制修复窗口中的所有控件，包括深层嵌套的控件
        /// </summary>
        private static void ForceFixAllControls(DependencyObject parent)
        {
            try
            {
                // 递归修复所有子控件
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    // 如果是 Border，强制修复
                    if (child is System.Windows.Controls.Border border)
                    {
                        ForceFixBorder(border);
                    }
                    
                    // 递归处理子控件
                    ForceFixAllControls(child);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceFixAllControls 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 强制修复 Border 控件
        /// </summary>
        private static void ForceFixBorder(System.Windows.Controls.Border border)
        {
            try
            {
                var cornerRadius = border.CornerRadius;
                
                if (cornerRadius != new System.Windows.CornerRadius(0))
                {
                    // 强制设置 BorderBrush
                    border.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    
                    var name = border.Name ?? "(未命名)";
                    System.Diagnostics.Debug.WriteLine($"ForceFixBorder: 强制修复 Border '{name}'");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceFixBorder 失败: {ex.Message}");
            }
        }
    }
}
