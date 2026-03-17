using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClassroomToolkit.App.Controls
{
    /// <summary>
    /// 安全的 Border 控件，自动处理 BorderBrush 问题
    /// </summary>
    public class SafeBorder : Border
    {
        public SafeBorder()
        {
            Loaded += OnSafeBorderLoaded;
        }

        private void OnSafeBorderLoaded(object sender, RoutedEventArgs e)
        {
            EnsureBorderBrush();
            Loaded -= OnSafeBorderLoaded;
        }

        private void EnsureBorderBrush()
        {
            // 如果有圆角但没有边框，自动设置透明边框
            if (CornerRadius != new CornerRadius(0) && BorderBrush == null)
            {
                BorderBrush = System.Windows.Media.Brushes.Transparent;
            }
        }
    }
}
