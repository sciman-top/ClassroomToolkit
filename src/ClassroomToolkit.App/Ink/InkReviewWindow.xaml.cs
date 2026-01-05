using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ClassroomToolkit.App.Ink;

public partial class InkReviewWindow : Window
{
    private readonly InkStrokeRenderer _renderer = new();

    public InkReviewWindow()
    {
        InitializeComponent();
    }

    public void LoadPage(InkPageData page, string backgroundPath)
    {
        if (!File.Exists(backgroundPath))
        {
            return;
        }
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(backgroundPath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        BackgroundImage.Source = bitmap;
        TitleText.Text = $"{page.DocumentName} - 第 {page.PageIndex} 页";

        var inkBitmap = _renderer.RenderPage(page, bitmap.PixelWidth, bitmap.PixelHeight, bitmap.DpiX, bitmap.DpiY);
        InkImage.Source = inkBitmap;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        try
        {
            DragMove();
        }
        catch
        {
            // Ignore drag exceptions.
        }
    }
}
