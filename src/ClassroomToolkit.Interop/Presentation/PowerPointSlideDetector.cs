using System;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace ClassroomToolkit.Interop.Presentation;

/// <summary>
/// Uses managed UI Automation (System.Windows.Automation) to detect current slide index.
/// This avoids AccessViolationException risks associated with manual COM usage.
/// </summary>
public static class PowerPointSlideDetector
{
    private static DateTime _lastAttempt = DateTime.MinValue;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Attempts to detect the current slide index from PowerPoint slideshow window using UI Automation.
    /// Returns null if detection fails.
    /// </summary>
    public static int? TryGetSlideIndex(IntPtr slideshowHwnd)
    {
        if (slideshowHwnd == IntPtr.Zero)
        {
            return null;
        }

        // Simple throttle to avoid hammering UIA which can be slow
        if (DateTime.UtcNow - _lastAttempt < ThrottleInterval)
        {
            return null;
        }
        _lastAttempt = DateTime.UtcNow;

        try
        {
            // Get AutomationElement from handle
            var windowElement = AutomationElement.FromHandle(slideshowHwnd);
            if (windowElement == null)
            {
                return null;
            }

            // Optimization: Use FindAll with a condition to get relevant elements directly
            // This is often more reliable than manual TreeWalker traversal for deep hierarchies
            
            System.Diagnostics.Debug.WriteLine($"[UIAutomation] Starting FindAll scan for hwnd={slideshowHwnd}");

            // Look for Text components, Edit components, or Status Bar items
            // PowerPoint status bar info is often in a Text or Button control within the Status Bar
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.StatusBar),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button) // Sometimes status items are buttons
            );

            var elements = windowElement.FindAll(TreeScope.Descendants, condition);
            System.Diagnostics.Debug.WriteLine($"[UIAutomation] FindAll found {elements.Count} elements");

            for (int i = 0; i < elements.Count; i++)
            {
                var el = elements[i];
                try 
                {
                    var name = el.Current.Name;
                    if (!string.IsNullOrEmpty(name))
                    {
                        // Log (throttle or debug only) to see what we are finding
                        // System.Diagnostics.Debug.WriteLine($"[UIAutomation] Element: {name}");
                    }
                    if (CheckNameForSlideIndex(name, out var idx))
                    {
                        System.Diagnostics.Debug.WriteLine($"[UIAutomation] Found slide index: {idx} in '{name}'");
                        return idx;
                    }
                }
                catch { }
            }
            
            // As a fallback, check the window name itself (e.g. for simple views)
            if (CheckNameForSlideIndex(windowElement.Current.Name, out var windowIdx))
            {
                return windowIdx;
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UIAutomation] Error: {ex.Message}");
            return null;
        }
    }

    // Removed manual FindSlideNumberRecursive static method as we use FindAll now
    
    public static int? TryGetSlideIndexFromAccessibility(IntPtr slideshowHwnd)
    {
        // Fallback to UIA implementation since managed UIA wraps MSAA internally often,
        // and we want to avoid manual P/Invoke that crashed.
        return TryGetSlideIndex(slideshowHwnd);
    }

    private static bool CheckNameForSlideIndex(string? name, out int slideIndex)
    {
        slideIndex = 0;
        if (string.IsNullOrEmpty(name)) return false;

        var patterns = new[]
        {
            @"^(\d+)\s*/\s*\d+$",              // "1/10"
            @"^(\d+)\s+of\s+\d+$",             // "1 of 10"
            @"^Slide\s+(\d+)$",                // "Slide 1"
            @"^第\s*(\d+)\s*张$",              // "第1张"
            @"幻灯片\s*(\d+)",
            // Loose match for presenter view
            @"Slide\s+(\d+)\s*of\s*\d+"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(name, pattern, RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var idx))
            {
                slideIndex = idx;
                return true;
            }
        }
        return false;
    }
}
