using System;
using System.Windows;
using System.Windows.Controls;

namespace ClassroomToolkit.App.Diagnostics
{
    /// <summary>
    /// 诊断工具，用于检测 BorderBrush 问题
    /// </summary>
    public class BorderBrushDiagnostic
    {
        public static void CheckAllBorders(Window window)
        {
            Console.WriteLine($"检查窗口: {window.GetType().Name}");
            CheckBordersRecursive(window, 0);
        }

        private static void CheckBordersRecursive(DependencyObject parent, int depth)
        {
            var indent = new string(' ', depth * 2);
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Border border)
                {
                    var cornerRadius = border.CornerRadius;
                    var borderBrush = border.BorderBrush;
                    
                    if (cornerRadius != new CornerRadius(0) && 
                        (borderBrush == null || borderBrush == DependencyProperty.UnsetValue))
                    {
                        Console.WriteLine($"{indent}❌ 发现问题 Border: CornerRadius={cornerRadius}, BorderBrush={borderBrush}");
                        
                        // 尝试修复
                        try
                        {
                            border.BorderBrush = Brushes.Transparent;
                            Console.WriteLine($"{indent}✅ 已修复: 设置 BorderBrush=Transparent");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{indent}❌ 修复失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{indent}✅ Border 正常: CornerRadius={cornerRadius}, BorderBrush={borderBrush}");
                    }
                }
                
                // 递归检查子元素
                CheckBordersRecursive(child, depth + 1);
            }
        }
    }
}
