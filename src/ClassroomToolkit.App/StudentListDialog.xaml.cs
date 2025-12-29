using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ClassroomToolkit.App.Models;

namespace ClassroomToolkit.App;

public partial class StudentListDialog : Window
{
    public StudentListDialog(IReadOnlyList<StudentListItem> students)
    {
        InitializeComponent();
        DataContext = new StudentListViewModel(students);
    }
    
    public int? SelectedIndex { get; private set; }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnStudentClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is StudentListItem item)
        {
            SelectedIndex = item.Index;
            DialogResult = true;
            Close();
        }
    }
}

public class StudentListViewModel
{
    public StudentListViewModel(IReadOnlyList<StudentListItem> students)
    {
        Students = students;
    }
    
    public IReadOnlyList<StudentListItem> Students { get; }
}