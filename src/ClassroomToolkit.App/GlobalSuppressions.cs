using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF window lifecycle is close-driven and owned disposable fields are released in closed/shutdown paths.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.MainWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF window lifecycle is close-driven and owned disposable fields are released in closed/shutdown paths.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.RollCallWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF window lifecycle is close-driven and owned disposable fields are released in closed/shutdown paths.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintOverlayWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF window lifecycle is close-driven and owned disposable fields are released in closed/shutdown paths.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.ImageManagerWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF window lifecycle is close-driven and owned disposable fields are released in closed/shutdown paths.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.PhotoOverlayWindow")]
