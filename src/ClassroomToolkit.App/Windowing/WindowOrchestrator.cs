using System;
using System.Collections.Generic;

namespace ClassroomToolkit.App.Windowing;

public sealed class WindowOrchestrator : IWindowOrchestrator
{
    public bool TouchSurface(IList<ZOrderSurface> surfaceStack, ZOrderSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surfaceStack);

        if (surface == ZOrderSurface.None)
        {
            return false;
        }

        if (surfaceStack.Count > 0 && surfaceStack[^1] == surface)
        {
            return false;
        }

        surfaceStack.Remove(surface);
        surfaceStack.Add(surface);
        return true;
    }

    public void PruneSurfaceStack(
        IList<ZOrderSurface> surfaceStack,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible)
    {
        ArgumentNullException.ThrowIfNull(surfaceStack);

        for (int i = surfaceStack.Count - 1; i >= 0; i--)
        {
            if (!IsSurfaceActive(surfaceStack[i], photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible))
            {
                surfaceStack.RemoveAt(i);
            }
        }
    }

    public ZOrderSurface ResolveFrontSurface(
        IReadOnlyList<ZOrderSurface> surfaceStack,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible)
    {
        ArgumentNullException.ThrowIfNull(surfaceStack);

        for (int i = surfaceStack.Count - 1; i >= 0; i--)
        {
            var surface = surfaceStack[i];
            if (IsSurfaceActive(surface, photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible))
            {
                return surface;
            }
        }

        if (imageManagerVisible)
        {
            return ZOrderSurface.ImageManager;
        }
        if (photoActive)
        {
            return ZOrderSurface.PhotoFullscreen;
        }
        if (presentationFullscreen)
        {
            return ZOrderSurface.PresentationFullscreen;
        }
        if (whiteboardActive)
        {
            return ZOrderSurface.Whiteboard;
        }

        return ZOrderSurface.None;
    }

    private static bool IsSurfaceActive(
        ZOrderSurface surface,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible)
    {
        return surface switch
        {
            ZOrderSurface.PhotoFullscreen => photoActive,
            ZOrderSurface.PresentationFullscreen => presentationFullscreen,
            ZOrderSurface.Whiteboard => whiteboardActive,
            ZOrderSurface.ImageManager => imageManagerVisible,
            _ => false
        };
    }
}
