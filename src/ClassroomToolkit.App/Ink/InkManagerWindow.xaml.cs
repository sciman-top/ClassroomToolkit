using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfButton = System.Windows.Controls.Button;

namespace ClassroomToolkit.App.Ink;

public partial class InkManagerWindow : Window
{
    private readonly InkStorageService _storage;
    private readonly ObservableCollection<InkPageView> _pages;
    private readonly ObservableCollection<HistoryDocumentView> _documents;
    private readonly List<HistoryDocumentView> _allDocuments;
    private DocumentCategory _currentCategory = DocumentCategory.Ppt;
    private string _currentDocument = string.Empty;
    private DateTime _currentDate = DateTime.Today;
    private string _currentSort = "recent";
    private string _currentSearch = string.Empty;

    public event Action<DateTime, string, int>? PageSelected;

    public InkManagerWindow()
    {
        InitializeComponent();
        _storage = new InkStorageService();
        _pages = new ObservableCollection<InkPageView>();
        _documents = new ObservableCollection<HistoryDocumentView>();
        _allDocuments = new List<HistoryDocumentView>();
        PageList.ItemsSource = _pages;
        DocumentList.ItemsSource = _documents;
        SortCombo.SelectedIndex = 0;
        ReloadDocuments();
        ApplyDocumentFilter();
    }

    private void ReloadDocuments()
    {
        _allDocuments.Clear();
        var latestDocuments = new Dictionary<string, HistoryDocumentView>(StringComparer.OrdinalIgnoreCase);
        foreach (var date in _storage.ListDates().OrderByDescending(d => d))
        {
            foreach (var docName in _storage.ListDocuments(date))
            {
                var pages = _storage.ListPages(date, docName);
                if (pages.Count == 0)
                {
                    continue;
                }
                var category = ResolveCategory(pages);
                var key = $"{category}:{docName}";
                if (latestDocuments.TryGetValue(key, out var existing) && existing.Date >= date)
                {
                    continue;
                }
                latestDocuments[key] = new HistoryDocumentView(docName, date, category);
            }
        }
        _allDocuments.AddRange(latestDocuments.Values);
    }

    private void ApplyDocumentFilter()
    {
        _documents.Clear();
        IEnumerable<HistoryDocumentView> filtered = _allDocuments
            .Where(doc => doc.Category == _currentCategory);
        if (!string.IsNullOrWhiteSpace(_currentSearch))
        {
            filtered = filtered.Where(doc =>
                doc.DocumentName.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase));
        }
        filtered = _currentSort switch
        {
            "name" => filtered.OrderBy(doc => doc.DocumentName, StringComparer.OrdinalIgnoreCase)
                              .ThenByDescending(doc => doc.Date),
            _ => filtered.OrderByDescending(doc => doc.Date)
                         .ThenBy(doc => doc.DocumentName, StringComparer.OrdinalIgnoreCase)
        };
        foreach (var doc in filtered)
        {
            _documents.Add(doc);
        }
        if (_documents.Count > 0)
        {
            DocumentList.SelectedIndex = 0;
        }
        else
        {
            _pages.Clear();
            DocumentTitle.Text = "暂无记录";
        }
    }

    private void ReloadPages()
    {
        _pages.Clear();
        if (string.IsNullOrWhiteSpace(_currentDocument))
        {
            DocumentTitle.Text = "请选择课件";
            return;
        }
        DocumentTitle.Text = $"{_currentDocument} · {_currentDate:yyyy-MM-dd}";
        var pages = _storage.ListPages(_currentDate, _currentDocument);
        foreach (var page in pages)
        {
            var imagePath = _storage.GetPageImagePath(_currentDate, _currentDocument, page.PageIndex);
            _pages.Add(new InkPageView(page.PageIndex, imagePath));
        }
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentCategory = CategoryTabs.SelectedIndex == 1 ? DocumentCategory.Image : DocumentCategory.Ppt;
        ApplyDocumentFilter();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _currentSearch = SearchBox.Text?.Trim() ?? string.Empty;
        ApplyDocumentFilter();
    }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _currentSort = tag;
        }
        else
        {
            _currentSort = "recent";
        }
        ApplyDocumentFilter();
    }

    private void OnDocumentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentList.SelectedItem is not HistoryDocumentView doc)
        {
            return;
        }
        _currentDocument = doc.DocumentName;
        _currentDate = doc.Date;
        ReloadPages();
    }

    private void OnPageClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not int pageIndex)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentDocument))
        {
            return;
        }
        PageSelected?.Invoke(_currentDate, _currentDocument, pageIndex);
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
            Activate();
            Mouse.Capture(null);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignore drag exceptions.
                }
            }), DispatcherPriority.Input);
        }
        catch
        {
            // Ignore drag exceptions.
        }
    }

    private static DocumentCategory ResolveCategory(IReadOnlyList<InkPageData> pages)
    {
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.SourcePath))
            {
                continue;
            }
            var ext = Path.GetExtension(page.SourcePath);
            if (IsImageExtension(ext))
            {
                return DocumentCategory.Image;
            }
            if (IsPptExtension(ext))
            {
                return DocumentCategory.Ppt;
            }
        }
        return DocumentCategory.Ppt;
    }

    private static bool IsImageExtension(string? ext)
    {
        return ext != null && (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPptExtension(string? ext)
    {
        return ext != null && (ext.Equals(".ppt", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".pptm", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".pps", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".ppsx", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record InkPageView(int PageIndex, string ThumbnailPath)
    {
        public string PageLabel => $"第 {PageIndex} 页";
    }

    private sealed record HistoryDocumentView(string DocumentName, DateTime Date, DocumentCategory Category)
    {
        public string DateLabel => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private enum DocumentCategory
    {
        Ppt,
        Image
    }
}
