using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// WPS Presentation COM state machine for reliable slide tracking.
/// 
/// Key design decisions based on verified WPS behaviors:
/// 1. GetActiveObject is unstable on WPS (officially acknowledged) - minimize repeated calls
/// 2. CreateInstance creates NEW empty instance, not connecting to existing one
/// 3. SlideShowWindows only contains slideshows started via COM SlideShowSettings.Run()
/// 4. SlideShowWindows is cleared immediately when slideshow ends
/// 5. Must hold COM reference during slideshow, avoid re-acquiring via GetActiveObject
/// 
/// State machine:
/// - Idle: No WPS slideshow detected
/// - Detecting: WPS detected, checking if slideshow is running (1-2 second window)
/// - ActiveSlideshow: Slideshow via COM Run(), can track pages reliably
/// - DegradedMode: Manual F5 slideshow (SlideShowWindows.Count = 0), use session-level canvas
/// </summary>
public sealed class WpsSlideShowTracker : IDisposable
{
    public enum TrackingState
    {
        Idle,
        Detecting,
        ActiveSlideshow,
        DegradedMode
    }

    // COM object references - held during slideshow to avoid repeated GetActiveObject
    private object? _wpsApp;
    private object? _slideShowWindow;
    private DateTime _lastConnectionTime = DateTime.MinValue;
    private DateTime _detectionStartTime = DateTime.MinValue;
    
    // Detection parameters
    private const int DetectionWindowMs = 2000; // 2 seconds to detect slideshow
    private const int MaxConnectionAgeMinutes = 10;
    
    // Current state
    private TrackingState _state = TrackingState.Idle;
    private string _currentPresentationName = string.Empty;
    private string _currentPresentationPath = string.Empty;
    private int _lastKnownSlideIndex = 0;
    private int _lastKnownSlideID = 0;
    private int _lastKnownShowPosition = 0;
    
    // Events
    public event Action<int, int, int>? SlideChanged; // slideIndex, slideID, showPosition
    public event Action<string, string, int, int>? SlideshowStarted; // presentationName, presentationPath, slideIndex, slideID
    public event Action? SlideshowEnded;
    public event Action? EnteredDegradedMode;
    
    public TrackingState State => _state;
    public string CurrentPresentationName => _currentPresentationName;
    public string CurrentPresentationPath => _currentPresentationPath;
    public int CurrentSlideIndex => _lastKnownSlideIndex;
    public int CurrentSlideID => _lastKnownSlideID;
    public bool IsTrackingActive => _state == TrackingState.ActiveSlideshow;
    public bool IsDegraded => _state == TrackingState.DegradedMode;
    
    /// <summary>
    /// Call this periodically (e.g., every 200-500ms) to poll slideshow state.
    /// </summary>
    public void Poll()
    {
        try
        {
            switch (_state)
            {
                case TrackingState.Idle:
                    TryDetectWpsSlideshow();
                    break;
                    
                case TrackingState.Detecting:
                    ContinueDetection();
                    break;
                    
                case TrackingState.ActiveSlideshow:
                    PollActiveSlideshow();
                    break;
                    
                case TrackingState.DegradedMode:
                    CheckDegradedModeExit();
                    break;
            }
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"[WpsTracker] COM Exception in Poll: {ex.Message}");
            HandleComError();
        }
        catch (Exception ex) when (ex.Message.Contains("RPC") || ex.Message.Contains("disconnected"))
        {
            Debug.WriteLine($"[WpsTracker] Disconnected in Poll: {ex.Message}");
            HandleComError();
        }
    }
    
    /// <summary>
    /// Start slideshow via COM. This is the preferred way to enable page tracking.
    /// Returns true if slideshow was started successfully.
    /// </summary>
    public bool StartSlideshow()
    {
        Debug.WriteLine("[WpsTracker] StartSlideshow called");
        try
        {
            // Force fresh connection attempt
            _wpsApp = null;
            _lastConnectionTime = DateTime.MinValue;
            
            EnsureConnection();
            if (_wpsApp == null)
            {
                Debug.WriteLine("[WpsTracker] Cannot start slideshow: no WPS connection");
                return false;
            }
            
            dynamic wps = _wpsApp;
            int presCount = 0;
            try { presCount = wps.Presentations.Count; } catch (Exception ex) { Debug.WriteLine($"[WpsTracker] Failed to get Presentations.Count: {ex.Message}"); }
            Debug.WriteLine($"[WpsTracker] Presentations.Count = {presCount}");
            
            if (presCount == 0)
            {
                Debug.WriteLine("[WpsTracker] Cannot start slideshow: no presentation open");
                return false;
            }
            
            dynamic pres = wps.ActivePresentation;
            if (pres == null)
            {
                Debug.WriteLine("[WpsTracker] Cannot start slideshow: ActivePresentation is null");
                return false;
            }
            
            Debug.WriteLine($"[WpsTracker] Starting slideshow via COM for: {pres.Name}");
            _slideShowWindow = pres.SlideShowSettings.Run();
            
            // Wait briefly for slideshow to initialize
            System.Threading.Thread.Sleep(500);
            
            int slideShowCount = 0;
            try { slideShowCount = wps.SlideShowWindows.Count; } catch { }
            Debug.WriteLine($"[WpsTracker] After Run(), SlideShowWindows.Count = {slideShowCount}");
            
            if (slideShowCount > 0)
            {
                _currentPresentationName = pres.Name ?? string.Empty;
                try { _currentPresentationPath = pres.FullName ?? string.Empty; } catch { }
                _state = TrackingState.ActiveSlideshow;
                ReadCurrentSlide();
                SlideshowStarted?.Invoke(_currentPresentationName, _currentPresentationPath, _lastKnownSlideIndex, _lastKnownSlideID);
                Debug.WriteLine($"[WpsTracker] Slideshow started successfully, tracking active, slide={_lastKnownSlideIndex}, slideID={_lastKnownSlideID}");
                return true;
            }
            
            Debug.WriteLine("[WpsTracker] Slideshow started but SlideShowWindows.Count is 0");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WpsTracker] Failed to start slideshow: {ex.Message}");
            return false;
        }
    }
    
    private void TryDetectWpsSlideshow()
    {
        // Check if WPS process is running
        if (!IsWpsRunning())
        {
            return;
        }
        
        // Try to connect to WPS
        EnsureConnection();
        if (_wpsApp == null)
        {
            return;
        }
        
        dynamic wps = _wpsApp;
        
        // Check if slideshow is already running
        int slideShowCount = 0;
        try { slideShowCount = wps.SlideShowWindows.Count; } catch { }
        
        if (slideShowCount > 0)
        {
            // Slideshow detected (either via COM Run or unlikely manual F5 scenario)
            Debug.WriteLine($"[WpsTracker] Slideshow detected, SlideShowWindows.Count = {slideShowCount}");
            _currentPresentationName = GetCurrentPresentationName(wps);
            _currentPresentationPath = GetCurrentPresentationPath(wps);
            _state = TrackingState.ActiveSlideshow;
            ReadCurrentSlide();
            SlideshowStarted?.Invoke(_currentPresentationName, _currentPresentationPath, _lastKnownSlideIndex, _lastKnownSlideID);
        }
        else
        {
            // No slideshow via COM - check if WPS appears to be in slideshow mode
            // (e.g., via window detection, fullscreen state check)
            // For now, we stay in Idle - user should use our "Start Slideshow" button
        }
    }
    
    private void ContinueDetection()
    {
        // Detection window expired
        if (DateTime.UtcNow - _detectionStartTime > TimeSpan.FromMilliseconds(DetectionWindowMs))
        {
            Debug.WriteLine("[WpsTracker] Detection window expired, entering degraded mode");
            _state = TrackingState.DegradedMode;
            EnteredDegradedMode?.Invoke();
            return;
        }
        
        // Still detecting - check for slideshow
        if (_wpsApp == null)
        {
            return;
        }
        
        dynamic wps = _wpsApp;
        int slideShowCount = 0;
        try { slideShowCount = wps.SlideShowWindows.Count; } catch { }
        
        if (slideShowCount > 0)
        {
            _currentPresentationName = GetCurrentPresentationName(wps);
            _currentPresentationPath = GetCurrentPresentationPath(wps);
            _state = TrackingState.ActiveSlideshow;
            ReadCurrentSlide();
            SlideshowStarted?.Invoke(_currentPresentationName, _currentPresentationPath, _lastKnownSlideIndex, _lastKnownSlideID);
        }
    }
    
    private void PollActiveSlideshow()
    {
        if (_wpsApp == null)
        {
            HandleSlideshowEnd();
            return;
        }
        
        dynamic wps = _wpsApp;
        
        // Check if slideshow is still running
        int slideShowCount = 0;
        try { slideShowCount = wps.SlideShowWindows.Count; } catch { }
        
        if (slideShowCount == 0)
        {
            // Slideshow ended
            HandleSlideshowEnd();
            return;
        }
        
        // Read current slide and detect changes
        ReadCurrentSlide();
    }
    
    private void ReadCurrentSlide()
    {
        if (_wpsApp == null) return;
        
        try
        {
            dynamic wps = _wpsApp;
            if (wps.SlideShowWindows.Count == 0) return;
            
            dynamic view = wps.SlideShowWindows[1].View;
            dynamic slide = view.Slide;
            
            int showPosition = 0;
            int slideIndex = 0;
            int slideID = 0;
            
            try { showPosition = (int)view.CurrentShowPosition; } catch { }
            try { slideIndex = (int)slide.SlideIndex; } catch { }
            try { slideID = (int)slide.SlideID; } catch { }
            
            // First read after slideshow start - always update
            bool isFirstRead = _lastKnownSlideIndex == 0 && _lastKnownSlideID == 0;
            
            // Detect real page change (not animation)
            // Use slideIndex as fallback if slideID is not available
            bool pageChanged = isFirstRead ||
                              (slideID > 0 && slideID != _lastKnownSlideID) ||
                              (slideID == 0 && slideIndex > 0 && slideIndex != _lastKnownSlideIndex) ||
                              (showPosition > 0 && showPosition != _lastKnownShowPosition);
            
            if (pageChanged)
            {
                Debug.WriteLine($"[WpsTracker] Page changed: idx {_lastKnownSlideIndex} -> {slideIndex}, showPos {_lastKnownShowPosition} -> {showPosition}, slideID {_lastKnownSlideID} -> {slideID}");
                _lastKnownShowPosition = showPosition;
                _lastKnownSlideIndex = slideIndex;
                _lastKnownSlideID = slideID;
                SlideChanged?.Invoke(slideIndex, slideID, showPosition);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WpsTracker] Failed to read current slide: {ex.Message}");
        }
    }
    
    private void HandleSlideshowEnd()
    {
        Debug.WriteLine("[WpsTracker] Slideshow ended");
        var lastSlideIndex = _lastKnownSlideIndex;
        var lastSlideID = _lastKnownSlideID;
        
        _state = TrackingState.Idle;
        _slideShowWindow = null;
        _lastKnownSlideIndex = 0;
        _lastKnownSlideID = 0;
        _lastKnownShowPosition = 0;
        
        SlideshowEnded?.Invoke();
    }
    
    private void CheckDegradedModeExit()
    {
        // In degraded mode, check if WPS slideshow detection windows appear
        // For now, stay in degraded mode until WPS is closed
        if (!IsWpsRunning())
        {
            _state = TrackingState.Idle;
            _wpsApp = null;
        }
    }
    
    private void EnsureConnection()
    {
        // Check if existing connection is still valid
        if (_wpsApp != null && DateTime.UtcNow - _lastConnectionTime < TimeSpan.FromMinutes(MaxConnectionAgeMinutes))
        {
            try
            {
                dynamic wps = _wpsApp;
                var _ = wps.Name; // Test connection
                return;
            }
            catch
            {
                _wpsApp = null;
            }
        }
        
        // Try to connect via GetActiveObject (NOT CreateInstance)
        // Try KWPP.Application first (Newer WPS)
        if (TryConnectProgId("KWPP.Application")) return;
        
        // Try WPP.Application fallback (Older/Enterprise WPS)
        if (TryConnectProgId("WPP.Application")) return;
    }
    
    private bool TryConnectProgId(string progId)
    {
        try
        {
            var clsidHr = CLSIDFromProgID(progId, out var clsid);
            Debug.WriteLine($"[WpsTracker] CLSIDFromProgID({progId}) HR=0x{clsidHr:X8}, CLSID={clsid}");
            
            if (clsidHr == 0)
            {
                var hr = GetActiveObject(ref clsid, IntPtr.Zero, out var result);
                Debug.WriteLine($"[WpsTracker] GetActiveObject HR=0x{hr:X8}, result={(result == null ? "null" : "object")}");
                
                if (hr == 0 && result != null)
                {
                    _wpsApp = result;
                    _lastConnectionTime = DateTime.UtcNow;
                    Debug.WriteLine($"[WpsTracker] Connected to WPS via GetActiveObject ({progId})");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[WpsTracker] GetActiveObject failed for {progId}: HR=0x{hr:X8}");
                }
            }
            else
            {
                Debug.WriteLine($"[WpsTracker] CLSIDFromProgID failed for {progId}: HR=0x{clsidHr:X8}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WpsTracker] Failed to connect {progId}: {ex.Message}");
        }
        return false;
    }

    private static bool IsWpsRunning()
    {
        try
        {
            if (Process.GetProcessesByName("wpp").Length > 0) return true;
            if (Process.GetProcessesByName("wps").Length > 0) return true;
            return false;
        }
        catch
        {
            return false;
        }
    }
    


    private void HandleComError()
    {
        _wpsApp = null;
        _slideShowWindow = null;
        
        if (_state == TrackingState.ActiveSlideshow)
        {
            HandleSlideshowEnd();
        }
        else
        {
            _state = TrackingState.Idle;
        }
    }
    
    private static string GetCurrentPresentationName(dynamic wps)
    {
        try
        {
            var pres = wps.ActivePresentation;
            return pres?.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private static string GetCurrentPresentationPath(dynamic wps)
    {
        try
        {
            var pres = wps.ActivePresentation;
            return pres?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    public void Dispose()
    {
        _wpsApp = null;
        _slideShowWindow = null;
    }
    
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object? result);
}
