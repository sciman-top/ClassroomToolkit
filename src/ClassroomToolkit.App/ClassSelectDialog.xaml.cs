using System.Windows;
using System.Windows.Input;

namespace ClassroomToolkit.App;

public partial class ClassSelectDialog : Window
{
    public string? SelectedClass { get; private set; }

    public ClassSelectDialog(IReadOnlyList<string> classNames, string? currentClass)
    {
        InitializeComponent();
        var source = classNames ?? Array.Empty<string>();
        ClassList.ItemsSource = source;
        if (!string.IsNullOrWhiteSpace(currentClass))
        {
            foreach (var name in source)
            {
                if (name.Equals(currentClass, StringComparison.OrdinalIgnoreCase))
                {
                    ClassList.SelectedItem = name;
                    break;
                }
            }
        }
        ClassList.MouseDoubleClick += OnClassDoubleClick;
        ClassList.KeyDown += OnClassKeyDown;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (ClassList.SelectedItem is string name)
        {
            SelectedClass = name;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnClassDoubleClick(object? sender, MouseButtonEventArgs e)
    {
        if (ClassList.SelectedItem is string)
        {
            OnConfirm(sender ?? this, new RoutedEventArgs());
        }
    }

    private void OnClassKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ClassList.SelectedItem is string)
        {
            OnConfirm(sender ?? this, new RoutedEventArgs());
        }
    }
}
