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
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF application and window root types are activated through generated XAML and app composition, so they remain public framework entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.App")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF application and window root types are activated through generated XAML and app composition, so they remain public framework entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.MainWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF application and window root types are activated through generated XAML and app composition, so they remain public framework entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.RollCallWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.AboutDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.AutoExitDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.ClassSelectDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.LauncherBubbleWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.RemoteKeyDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.RollCallSettingsDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.StudentListDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.TimerSetDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Diagnostic result data is shared between UI dialogs, export helpers, and tests as a stable compatibility DTO.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Diagnostics.DiagnosticsResult")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Diagnostics.DiagnosticsDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Diagnostics.StartupCompatibilityWarningDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Ink export service is instantiated directly in tests and app startup, and its public surface represents the composite export compatibility boundary.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkExportService")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Ink persistence service is instantiated directly in tests and app startup, and its public surface represents the sidecar persistence contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkPersistenceService")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Ink.InkSettingsDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Factory interfaces appear in MainWindow constructor injection and application composition, so they remain public DI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.IRollCallWindowFactory")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound model items are referenced by templates, bindings, dialogs, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Models.GroupButtonItem")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound model items are referenced by templates, bindings, dialogs, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Models.StudentListItem")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted paint preset enums participate in settings and UI selection contracts, so they remain public compatibility enums.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.WhiteboardBrushPreset")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted paint preset enums participate in settings and UI selection contracts, so they remain public compatibility enums.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.CalligraphyBrushPreset")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted paint/render enums participate in settings, ink schema, and UI selection contracts, so they remain public compatibility enums.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.CalligraphyRenderMode")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted paint/render enums participate in settings, ink schema, and UI selection contracts, so they remain public compatibility enums.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.ClassroomWritingMode")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted paint/render enums participate in settings, ink schema, and UI selection contracts, so they remain public compatibility enums.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintBrushStyle")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted paint/render enums participate in settings, ink schema, and UI selection contracts, so they remain public compatibility enums.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintShapeType")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Persisted paint/render enums participate in settings, ink schema, and UI selection contracts, so they remain public compatibility enums.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintToolMode")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Factory interfaces appear in MainWindow constructor injection and application composition, so they remain public DI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.IPaintWindowFactory")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.BoardColorDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintOverlayWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintSettingsDialog")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.PaintToolbarWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.QuickColorPaletteWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Paint.RegionSelectionOverlayWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Paint window orchestration is consumed through MainWindow constructor injection and explicit event contracts, so it remains public at the composition boundary.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Services.IPaintWindowOrchestrator")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Settings service is instantiated directly in tests and app startup, and its public surface represents the settings document compatibility boundary.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Settings.AppSettingsService")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound view models and item records are referenced by bindings, templates, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.FolderItem")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound view models and item records are referenced by bindings, templates, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.ImageItem")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound view models and item records are referenced by bindings, templates, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.ImageManagerViewModel")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Factory interfaces appear in MainWindow constructor injection and application composition, so they remain public DI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.IImageManagerWindowFactory")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.ImageManagerWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.PhotoOverlayWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WPF dialog/window types are activated through generated XAML or explicit window composition, so they remain public UI entrypoints.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.RollCallGroupOverlayWindow")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "The custom virtualizing panel is referenced directly from XAML and validated by touch-flow tests, so it remains a public WPF component contract.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.VirtualizingWrapPanel")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Behaviors.LongPressBehavior")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Converters.InverseBooleanToVisibilityConverter")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Converters.PdfFontWeightConverter")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Converters.PdfForegroundConverter")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.MultiplyConverter")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.FolderVisibilityConverter")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.FileVisibilityConverter")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML behaviors, controls, and converters are referenced directly from markup resources and templates, so they remain public WPF component contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Photos.PdfBackgroundConverter")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound view models and item records are referenced by bindings, templates, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.ViewModels.ViewModelBase")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound view models and item records are referenced by bindings, templates, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.ViewModels.MainViewModel")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "XAML-bound view models and item records are referenced by bindings, templates, and tests, so they remain public UI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.ViewModels.RollCallViewModel")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Factory interfaces appear in MainWindow constructor injection and application composition, so they remain public DI contracts.",
    Scope = "type",
    Target = "~T:ClassroomToolkit.App.Windowing.IWindowOrchestrator")]
