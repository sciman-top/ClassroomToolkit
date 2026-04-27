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
[assembly: SuppressMessage(
    "Security",
    "CA5394:Do not use insecure randomness",
    Justification = "Random values here only generate deterministic visual ink texture variation; they are not used for security, identifiers, or persistence secrets.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintOverlayWindow")]
[assembly: SuppressMessage(
    "Security",
    "CA5394:Do not use insecure randomness",
    Justification = "Random values here only generate deterministic visual ink texture variation; they are not used for security, identifiers, or persistence secrets.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkStrokeRenderer")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "WPF window continuations must resume on the dispatcher thread to update bound UI state safely.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.ImageManagerWindow")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "WPF window continuations must resume on the dispatcher thread to update bound UI state safely.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.PhotoOverlayWindow")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "WPF window continuations must resume on the dispatcher thread to update bound UI state safely.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.RollCallWindow")]
[assembly: SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "WPF coordinator continuations must preserve the UI synchronization context before touching window state.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.RollCall.RollCallRemoteHookCoordinator")]
[assembly: SuppressMessage(
    "Performance",
    "CA1802:Use literals where appropriate",
    Justification = "Keep the feature flag non-const so fallback branches remain compile-checked and easy to re-enable.",
    Scope = "member",
    Target = "~F:ClassroomToolkit.App.Paint.PaintOverlayWindow.CalligraphySinglePassCompositeEnabled")]
[assembly: SuppressMessage(
    "Naming",
    "CA1724:Type names should not match namespaces",
    Justification = "WPF App is the framework conventional application class name and is referenced by generated XAML startup code.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.App")]
[assembly: SuppressMessage(
    "Design",
    "CA1030:Use events where appropriate",
    Justification = "RelayCommand follows the WPF ICommand pattern where RaiseCanExecuteChanged is an imperative notification helper.",
    Scope = "member",
    Target = "~M:ClassroomToolkit.App.Commands.RelayCommand.RaiseCanExecuteChanged")]
[assembly: SuppressMessage(
    "Design",
    "CA1030:Use events where appropriate",
    Justification = "RaisePropertyChanged is a ViewModel helper for batching existing INotifyPropertyChanged events.",
    Scope = "member",
    Target = "~M:ClassroomToolkit.App.ViewModels.ViewModelBase.RaisePropertyChanged(System.String[])")]
[assembly: SuppressMessage(
    "Design",
    "CA1034:Nested types should not be visible",
    Justification = "Nested export result keeps the existing public result contract colocated with the export service.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkExportService.InkExportRunResult")]
[assembly: SuppressMessage(
    "Design",
    "CA1034:Nested types should not be visible",
    Justification = "Geometry DTOs are intentionally nested under the renderer to avoid broadening the brush API surface.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.Brushes.VariableWidthBrushRenderer.RibbonGeometry")]
[assembly: SuppressMessage(
    "Design",
    "CA1034:Nested types should not be visible",
    Justification = "Geometry DTOs are intentionally nested under the renderer to avoid broadening the brush API surface.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.Brushes.VariableWidthBrushRenderer.InkBloomGeometry")]
[assembly: SuppressMessage(
    "Globalization",
    "CA1303:Do not pass literals as localized parameters",
    Justification = "The app currently stores classroom UI copy inline; this dialog text must remain consistent until localization resources are introduced.",
    Scope = "member",
    Target = "~M:ClassroomToolkit.App.Photos.ImageManagerWindow.OnAddFavoriteClick(System.Object,System.Windows.RoutedEventArgs)")]
