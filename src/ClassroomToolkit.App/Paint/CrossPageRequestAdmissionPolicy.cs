namespace ClassroomToolkit.App.Paint;

internal enum CrossPageRequestAdmissionReason
{
    None = 0,
    CrossPageInactive = 1,
    PhotoLoading = 2,
    BackgroundNotReady = 3,
    OverlayNotVisible = 4,
    OverlayMinimized = 5,
    ViewportUnavailable = 6
}

internal readonly record struct CrossPageRequestAdmissionDecision(
    bool ShouldAdmit,
    CrossPageRequestAdmissionReason Reason);

internal static class CrossPageRequestAdmissionPolicy
{
    internal static CrossPageRequestAdmissionDecision Resolve(
        bool crossPageDisplayActive,
        bool photoLoading,
        bool hasPhotoBackgroundSource,
        bool overlayVisible,
        bool overlayMinimized,
        bool hasUsableViewport)
    {
        if (!crossPageDisplayActive)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.CrossPageInactive);
        }

        if (photoLoading)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.PhotoLoading);
        }

        if (!hasPhotoBackgroundSource)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.BackgroundNotReady);
        }

        if (!overlayVisible)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.OverlayNotVisible);
        }

        if (overlayMinimized)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.OverlayMinimized);
        }

        if (!hasUsableViewport)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.ViewportUnavailable);
        }

        return new CrossPageRequestAdmissionDecision(
            ShouldAdmit: true,
            Reason: CrossPageRequestAdmissionReason.None);
    }

    internal static bool ShouldAdmit(
        bool crossPageDisplayActive,
        bool photoLoading,
        bool hasPhotoBackgroundSource,
        bool overlayVisible,
        bool overlayMinimized,
        bool hasUsableViewport)
    {
        return Resolve(
            crossPageDisplayActive,
            photoLoading,
            hasPhotoBackgroundSource,
            overlayVisible,
            overlayMinimized,
            hasUsableViewport).ShouldAdmit;
    }
}
