using System;
using System.Windows;
using ClassroomToolkit.App.Helpers;

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
            BorderFixHelper.FixAllBorders(window);
        }
    }
}
