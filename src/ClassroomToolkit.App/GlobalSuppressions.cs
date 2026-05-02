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
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "App settings are a persisted compatibility contract consumed by the settings loaders, UI dialogs, and tests; narrowing visibility would not change runtime access but would obscure an intentional public data contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Settings.AppSettings")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Settings document format is exposed through IConfigurationService and persisted settings migration logic, so it remains part of the compatibility surface.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Settings.SettingsDocumentFormat")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "The configuration service interface appears in app composition and public constructor signatures; keeping it public avoids inconsistent accessibility across the WPF startup surface.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Settings.IConfigurationService")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted ink sidecar documents are a compatibility contract shared across storage, export, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkDocumentData")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Ink export scope is persisted in settings and exchanged between the paint settings dialog and export services, so it is an intentional public enum contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkExportScope")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Ink export options are registered in app composition and consumed by public-facing window wiring, so keeping the DTO public avoids inconsistent accessibility in the app startup graph.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkExportOptions")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted ink stroke metadata is part of the saved ink JSON schema and test fixture surface.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkStrokeType")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted ink bloom geometry is part of the saved ink JSON schema and must remain deserializable across sessions.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkBloomData")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted ink ribbon geometry is part of the saved ink JSON schema and must remain deserializable across sessions.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkRibbonData")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted ink strokes are consumed by storage, export, rendering, and tests, so the DTO remains an intentional public contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkStrokeData")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted ink pages are consumed by storage, export, rendering, and tests, so the DTO remains an intentional public contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkPageData")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Presentation source is emitted through public paint-window orchestration events and session restoration policies.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Presentation.PresentationForegroundSource")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Floating z-order requests are emitted through the public paint-window orchestration event contract and consumed by windowing policies and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Windowing.FloatingZOrderRequest")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Z-order surfaces are part of the public window orchestration contract and its tests, so they remain public until a broader API review changes that boundary.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Windowing.ZOrderSurface")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI session events and state records flow through public orchestration events, session coordinators, and tests, so they remain public compatibility contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiSessionEvent")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI session scene kind is part of the public session state contract used by orchestration, restoration policies, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiSceneKind")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI tool mode is part of the public session state contract used by orchestration, restoration policies, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiToolMode")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI navigation mode is part of the public session state contract used by orchestration, restoration policies, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiNavigationMode")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI focus owner is part of the public session state contract used by orchestration, restoration policies, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiFocusOwner")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI ink visibility is part of the public session state contract used by orchestration, restoration policies, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiInkVisibility")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Presentation source kind is part of the public session state contract used by orchestration, restoration policies, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.PresentationSourceKind")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Photo source kind is part of the public session state contract used by orchestration, restoration policies, and tests.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.PhotoSourceKind")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI session state flows through public overlay state accessors and orchestration events, so it remains an intentional public record contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiSessionState")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "UI session transitions flow through public overlay transition events and windowing policies, so they remain an intentional public record contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Session.UiSessionTransition")]
