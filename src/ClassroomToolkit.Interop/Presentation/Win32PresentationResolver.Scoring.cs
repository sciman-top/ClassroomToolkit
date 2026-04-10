namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class Win32PresentationResolver
{
    private PresentationWindowCheck? BuildWindowCheck(
        IntPtr hwnd,
        PresentationWindowInfo info,
        PresentationClassifier classifier)
    {
        var type = classifier.Classify(info);
        if (type is PresentationType.None or PresentationType.Other)
        {
            return null;
        }

        var classMatch = classifier.IsSlideshowWindow(info);
        var processMatch = type is PresentationType.Wps or PresentationType.Office;
        var hasCaption = HasCaption(hwnd);
        var isFullscreen = IsFullscreenWindow(hwnd);
        if (_scoringOptions.RequireClassMatchOrFullscreen && !classMatch && !isFullscreen)
        {
            return null;
        }

        var score = 0;
        if (classMatch)
        {
            score += _scoringOptions.ClassMatchWeight;
        }
        if (processMatch)
        {
            score += _scoringOptions.ProcessMatchWeight;
        }
        if (!hasCaption)
        {
            score += _scoringOptions.NoCaptionWeight;
        }
        if (isFullscreen)
        {
            score += _scoringOptions.IsFullscreenWeight;
        }
        if (score < _scoringOptions.MinimumCandidateScore)
        {
            return null;
        }

        return new PresentationWindowCheck(
            type,
            info.ProcessName,
            info.ClassNames,
            classMatch,
            processMatch,
            hasCaption,
            isFullscreen,
            score);
    }
}
