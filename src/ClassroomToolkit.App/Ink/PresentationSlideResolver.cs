using System;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Extended slide info including SlideID and CurrentShowPosition for reliable page change detection.
/// </summary>
/// <param name="FilePath">Full path to the presentation file.</param>
/// <param name="DisplayName">Display name of the presentation.</param>
/// <param name="SlideIndex">1-based slide index (may change with slide reordering).</param>
/// <param name="SlideID">Stable slide ID (remains constant even if slides are reordered). 0 if unavailable.</param>
/// <param name="CurrentShowPosition">Position in the current slideshow (useful for detecting page changes). 0 if unavailable.</param>
public sealed record PresentationSlideInfo(
    string FilePath,
    string DisplayName,
    int SlideIndex,
    int SlideID = 0,
    int CurrentShowPosition = 0);

public static class PresentationSlideResolver
{
    // Cache last COM failure to avoid repeated attempts
    private static DateTime _lastPptComFailure = DateTime.MinValue;
    private static DateTime _lastWpsComFailure = DateTime.MinValue;
    private static readonly TimeSpan ComRetryInterval = TimeSpan.FromSeconds(3);

    // Cached COM application objects to avoid repeated CreateInstance calls
    private static object? _cachedPptApp;
    private static object? _cachedWpsApp;
    private static DateTime _lastPptCacheTime = DateTime.MinValue;
    private static DateTime _lastWpsCacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheValidDuration = TimeSpan.FromMinutes(5);

    public static PresentationSlideInfo? TryResolvePowerPoint()
    {
        var result = TryResolvePowerPointWithApplication(out _);
        return result;
    }

    public static PresentationSlideInfo? TryResolvePowerPointWithApplication(out object? application)
    {
        application = null;
        // Skip if COM failed recently
        if (DateTime.UtcNow - _lastPptComFailure < ComRetryInterval)
        {
            return null;
        }

        var result = TryResolvePowerPointCore(out application);
        if (result != null)
        {
            return result;
        }

        // Mark failure to throttle retries
        _lastPptComFailure = DateTime.UtcNow;
        application = null;
        return null;
    }

    public static object? TryGetPowerPointApplication()
    {
        if (DateTime.UtcNow - _lastPptComFailure < ComRetryInterval)
        {
            return null;
        }

        try
        {
            var app = GetOrCreateApplication("PowerPoint.Application", ref _cachedPptApp, ref _lastPptCacheTime);
            if (app == null)
            {
                _lastPptComFailure = DateTime.UtcNow;
            }
            return app;
        }
        catch (COMException)
        {
            InvalidatePptCache();
            _lastPptComFailure = DateTime.UtcNow;
            return null;
        }
        catch (InvalidOleVariantTypeException)
        {
            InvalidatePptCache();
            _lastPptComFailure = DateTime.UtcNow;
            return null;
        }
        catch (InvalidCastException)
        {
            InvalidatePptCache();
            _lastPptComFailure = DateTime.UtcNow;
            return null;
        }
        catch (Exception ex) when (ex.Message.Contains("RPC") || ex.Message.Contains("disconnected"))
        {
            InvalidatePptCache();
            _lastPptComFailure = DateTime.UtcNow;
            return null;
        }
    }

    private static PresentationSlideInfo? TryResolvePowerPointCore(out object? application)
    {
        application = null;
        try
        {
            var app = GetOrCreateApplication("PowerPoint.Application", ref _cachedPptApp, ref _lastPptCacheTime);
            if (app == null)
            {
                return null;
            }

            application = app;
            return ExtractSlideInfo(app);
        }
        catch (COMException)
        {
            InvalidatePptCache();
            return null;
        }
        catch (InvalidOleVariantTypeException)
        {
            InvalidatePptCache();
            return null;
        }
        catch (InvalidCastException)
        {
            InvalidatePptCache();
            return null;
        }
        catch (Exception ex) when (ex.Message.Contains("RPC") || ex.Message.Contains("disconnected"))
        {
            // COM object disconnected, clear cache
            InvalidatePptCache();
            return null;
        }
    }

    public static PresentationSlideInfo? TryResolveWps()
    {
        // Skip if COM failed recently
        if (DateTime.UtcNow - _lastWpsComFailure < ComRetryInterval)
        {
            return null;
        }

        // Try KWPP first (newer WPS), then WPP (older)
        var info = TryResolveWpsFromProgId("KWPP.Application");
        if (info != null)
        {
            return info;
        }

        info = TryResolveWpsFromProgId("WPP.Application");
        if (info != null)
        {
            return info;
        }

        _lastWpsComFailure = DateTime.UtcNow;
        return null;
    }

    private static PresentationSlideInfo? TryResolveWpsFromProgId(string progId)
    {
        try
        {
            var app = GetOrCreateApplication(progId, ref _cachedWpsApp, ref _lastWpsCacheTime);
            if (app == null)
            {
                return null;
            }

            return ExtractSlideInfo(app);
        }
        catch (COMException)
        {
            InvalidateWpsCache();
            return null;
        }
        catch (InvalidOleVariantTypeException)
        {
            InvalidateWpsCache();
            return null;
        }
        catch (InvalidCastException)
        {
            InvalidateWpsCache();
            return null;
        }
        catch (Exception ex) when (ex.Message.Contains("RPC") || ex.Message.Contains("disconnected"))
        {
            InvalidateWpsCache();
            return null;
        }
    }

    /// <summary>
    /// Extracts slide information from the running application.
    /// Uses SlideShowWindows[1].View for slideshow mode (NOT ActiveWindow).
    /// </summary>
    private static PresentationSlideInfo? ExtractSlideInfo(object app)
    {
        dynamic ppt = app;

        // Check if slideshow is running
        var slideShowWindows = ppt.SlideShowWindows;
        if (slideShowWindows == null)
        {
            return null;
        }
        
        // Split check to avoid CS8602 warning and handle COM errors safely
        int count = 0;
        try { count = slideShowWindows.Count; } catch { }
        
        if (count < 1)
        {
            return null;
        }

        dynamic window = ppt.SlideShowWindows[1];
        dynamic view = window.View;
        if (view == null)
        {
            return null;
        }

        object? slideObj = view.Slide;
        if (slideObj == null)
        {
            return null;
        }

        dynamic slide = slideObj;

        // Get presentation info
        string filePath = string.Empty;
        string name = string.Empty;
        try
        {
            var activePresentation = ppt.ActivePresentation;
            if (activePresentation != null)
            {
                filePath = activePresentation.FullName ?? string.Empty;
                name = activePresentation.Name ?? string.Empty;
            }
        }
        catch
        {
            // Ignore if ActivePresentation is not available
        }

        // Get SlideIndex (required)
        int slideIndex;
        try
        {
            slideIndex = (int)slide.SlideIndex;
        }
        catch
        {
            return null;
        }

        if (slideIndex <= 0)
        {
            return null;
        }

        // Get SlideID (optional, more stable than SlideIndex)
        int slideID = 0;
        try
        {
            slideID = (int)slide.SlideID;
        }
        catch
        {
            // SlideID not available (some WPS versions may not support it)
        }

        // Get CurrentShowPosition (optional, useful for detecting page changes vs animations)
        int currentShowPosition = 0;
        try
        {
            currentShowPosition = (int)view.CurrentShowPosition;
        }
        catch
        {
            // CurrentShowPosition not available
        }

        return new PresentationSlideInfo(filePath, name, slideIndex, slideID, currentShowPosition);
    }

    /// <summary>
    /// Gets or creates COM application object. Uses CreateInstance as fallback when GetActiveObject fails.
    /// PowerPoint/WPS are single-instance apps, so CreateInstance connects to existing instance.
    /// NOTE: For WPS, CreateInstance is preferred over GetActiveObject per official community recommendation
    /// (GetActiveObject on WPS has known instability issues, officially acknowledged).
    /// </summary>
    private static object? GetOrCreateApplication(string progId, ref object? cached, ref DateTime cacheTime)
    {
        // Check if cached object is still valid
        if (cached != null && DateTime.UtcNow - cacheTime < CacheValidDuration)
        {
            // Verify cached object is still connected
            try
            {
                dynamic app = cached;
                var _ = app.Name; // Simple property access to check connectivity
                return cached;
            }
            catch
            {
                // Cached object disconnected, clear it
                cached = null;
            }
        }

        bool isWps = progId.StartsWith("KWPP", StringComparison.OrdinalIgnoreCase) ||
                     progId.StartsWith("WPP", StringComparison.OrdinalIgnoreCase);

        // IMPORTANT: For both PowerPoint AND WPS, GetActiveObject is required to connect to user's running instance.
        // CreateInstance creates a NEW empty instance (verified by testing on WPS), which is not what we want.
        // Official WPS community suggested CreateInstance, but testing shows it doesn't connect to existing instance.
        var result = TryGetActiveObject(progId, ref cached, ref cacheTime);
        if (result != null)
        {
            return result;
        }

        // Fallback to CreateInstance only for PowerPoint (which is truly single-instance)
        // WPS CreateInstance creates new instance, so skip it for WPS
        if (!isWps)
        {
            return TryCreateInstance(progId, ref cached, ref cacheTime);
        }

        return null;
    }

    private static object? TryGetActiveObject(string progId, ref object? cached, ref DateTime cacheTime)
    {
        if (CLSIDFromProgID(progId, out var clsid) != 0)
        {
            return null;
        }

        try
        {
            var hr = GetActiveObject(ref clsid, IntPtr.Zero, out var result);
            if (hr == 0 && result != null)
            {
                cached = result;
                cacheTime = DateTime.UtcNow;
                System.Diagnostics.Debug.WriteLine($"[SlideResolver] Connected via GetActiveObject: {progId}");
                return result;
            }
        }
        catch (InvalidOleVariantTypeException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SlideResolver] GetActiveObject failed for {progId}: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SlideResolver] GetActiveObject failed for {progId}: {ex.Message}");
        }

        return null;
    }

    private static object? TryCreateInstance(string progId, ref object? cached, ref DateTime cacheTime)
    {
        try
        {
            var type = Type.GetTypeFromProgID(progId, throwOnError: false);
            if (type == null)
            {
                return null;
            }

            // Check if application is actually running before creating instance
            // to avoid accidentally launching PowerPoint/WPS
            if (!IsApplicationRunning(progId))
            {
                System.Diagnostics.Debug.WriteLine($"[SlideResolver] {progId} process not running, skipping CreateInstance");
                return null;
            }

            var instance = Activator.CreateInstance(type);
            if (instance != null)
            {
                cached = instance;
                cacheTime = DateTime.UtcNow;
                System.Diagnostics.Debug.WriteLine($"[SlideResolver] Connected via CreateInstance: {progId}");
                return instance;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SlideResolver] CreateInstance failed for {progId}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Checks if the application is running by looking for its process.
    /// </summary>
    private static bool IsApplicationRunning(string progId)
    {
        string processName = progId.ToUpperInvariant() switch
        {
            "POWERPOINT.APPLICATION" => "POWERPNT",
            "KWPP.APPLICATION" => "wpp",
            "WPP.APPLICATION" => "wpp",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(processName))
        {
            return false;
        }

        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void InvalidatePptCache()
    {
        _cachedPptApp = null;
    }

    private static void InvalidateWpsCache()
    {
        _cachedWpsApp = null;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, out object? result);
}
