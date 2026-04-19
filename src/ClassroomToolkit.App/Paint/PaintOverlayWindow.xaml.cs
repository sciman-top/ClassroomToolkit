using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO;
using System.Threading;
using IoPath = System.IO.Path;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Application.UseCases.Photos;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.Services.Presentation;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfPath = System.Windows.Shapes.Path;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;
using System.Windows.Interop;
using System.Windows.Threading;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using Microsoft.Extensions.Logging;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow : Window
{
    private static DateTime GetCurrentUtcTimestamp() => DateTime.UtcNow;




    public PaintOverlayWindow()
        : this(null)
    {
    }

    public PaintOverlayWindow(ILogger<PaintOverlayWindow>? logger)
    {
        _logger = logger;
        InitializeComponent();
        _inkRendererFactory = InkRendererFactoryResolver.Resolve(
            AppFlags.UseGpuInkRenderer,
            out _);
        _neighborPagesCanvas = FindName("NeighborPagesCanvas") as System.Windows.Controls.Canvas;
        _visualHost = new DrawingVisualHost();
        CustomDrawHost.Child = _visualHost;
        _photoContentTransform = new TransformGroup();
        _photoContentTransform.Children.Add(_photoPageScale);
        _photoContentTransform.Children.Add(_photoScale);
        _photoContentTransform.Children.Add(_photoTranslate);
        PhotoBackground.RenderTransform = _photoContentTransform;
        RasterImage.RenderTransform = _photoInkPanCompensation;
        
        WindowStateTransitionExecutor.Apply(this, WindowState.Maximized);
        _refreshOrchestrator = new RefreshOrchestrator(Dispatcher, MonitorInkContext);
        _presentationFocusMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PresentationFocusMonitorIntervalMs)
        };
        _presentationFocusMonitor.Tick += OnPresentationFocusMonitorTick;
        _photoRenderQualityRestoreTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(PhotoRenderQualityRestoreDelayMs)
        };
        _photoRenderQualityRestoreTimer.Tick += OnPhotoRenderQualityRestoreTimerTick;
        _inkMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(InkMonitorActiveIntervalMs)
        };
        _inkMonitor.Tick += OnInkMonitorTick;
        _inkSidecarAutoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(InkSidecarAutoSaveDelayMs)
        };
        _inkSidecarAutoSaveTimer.Tick += OnInkSidecarAutoSaveTimerTick;
        _perfMonitor = new PerfStats("MonitorInkContext");
        _perfSavePage = new PerfStats("SavePage");
        _perfLoadPage = new PerfStats("LoadPage");
        _perfRedrawSurface = new PerfStats("RedrawSurface");
        _perfEnsureSurface = new PerfStats("EnsureSurface");
        _perfClearSurface = new PerfStats("ClearSurface");
        _perfApplyStrokes = new PerfStats("ApplyStrokes");
        
        KeyDown += OnKeyDown;
        Loaded += OnOverlayLoaded;
        IsVisibleChanged += OnOverlayVisibleChanged;
        SourceInitialized += OnOverlaySourceInitialized;
        Deactivated += OnOverlayDeactivated;
        OverlayRoot.MouseLeftButtonDown += OnMouseDown;
        OverlayRoot.MouseMove += OnMouseMove;
        OverlayRoot.MouseLeftButtonUp += OnMouseUp;
        OverlayRoot.MouseRightButtonDown += OnRightButtonDown;
        OverlayRoot.MouseRightButtonUp += OnRightButtonUp;
        OverlayRoot.MouseLeave += OnOverlayMouseLeave;
        OverlayRoot.LostMouseCapture += OnOverlayLostMouseCapture;
        OverlayRoot.IsManipulationEnabled = true;
        OverlayRoot.ManipulationStarting += OnManipulationStarting;
        OverlayRoot.ManipulationInertiaStarting += OnManipulationInertiaStarting;
        OverlayRoot.ManipulationDelta += OnManipulationDelta;
        OverlayRoot.ManipulationCompleted += OnManipulationCompleted;
        OverlayRoot.TouchDown += OnTouchDown;
        OverlayRoot.TouchMove += OnTouchMove;
        OverlayRoot.TouchUp += OnTouchUp;
        OverlayRoot.LostTouchCapture += OnOverlayLostTouchCapture;
        OverlayRoot.StylusDown += OnStylusDown;
        OverlayRoot.StylusMove += OnStylusMove;
        OverlayRoot.StylusUp += OnStylusUp;
        MouseWheel += OnMouseWheel;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        UpdateBoardBackground();
        ApplyPhotoRenderQualityMode(useLowQualityScaling: false, forceApply: true);

        _presentationClassifier = new PresentationClassifier();
        var planner = new ClassroomToolkit.Services.Presentation.PresentationControlPlanner(_presentationClassifier);
        var mapper = new ClassroomToolkit.Services.Presentation.PresentationCommandMapper();
        var sender = new Win32InputSender();
        _presentationResolver = new Win32PresentationResolver();
        _presentationService = new ClassroomToolkit.Services.Presentation.PresentationControlService(planner, mapper, sender, _presentationResolver, new ClassroomToolkit.Services.Presentation.Win32PresentationWindowValidator());
        _presentationOptions = new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = false,
            WpsDebounceMs = PresentationRuntimeDefaults.WpsNavDebounceMs,
            LockStrategyWhenDegraded = true,
            AutoFallbackFailureThreshold = ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault,
            AutoFallbackProbeIntervalCommands = ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault,
            AllowOffice = true,
            AllowWps = true
        };
        _presentationInputPipeline = new PresentationInputPipeline(
            _presentationService,
            _presentationOptions.Strategy,
            InputStrategy.Auto);
        _presentationTargetSnapshotProvider = new OverlayPresentationTargetSnapshotProvider(
            _presentationResolver,
            () => _presentationClassifier,
            IsFullscreenWindow,
            _currentProcessId);
        _presentationDispatchCoordinator = new OverlayPresentationDispatchCoordinator(_presentationTargetSnapshotProvider);
        _wpsNavHook = new WpsSlideshowNavigationHook();
        var navHook = _wpsNavHook;
        _wpsNavHookClient = navHook == null ? null : new WpsNavHookClient(navHook);
        if (navHook != null && navHook.Available)
        {
            navHook.NavigationRequested += OnWpsNavHookRequested;
        }
        _sessionCoordinator = new SessionCoordinator(
            new PaintOverlaySessionEffectRunner(
                applyOverlayTopmost: ApplySessionOverlayTopmost,
                applyNavigationMode: ApplySessionNavigationMode,
                applyInkVisibility: ApplySessionInkVisibility,
                applyWidgetVisibility: ApplySessionWidgetVisibility,
                onTransition: LogSessionTransition));
        DispatchSessionEvent(new SwitchToolModeEvent(MapSessionToolMode(_mode)));
        Closed += OnOverlayClosed;
    }

}
