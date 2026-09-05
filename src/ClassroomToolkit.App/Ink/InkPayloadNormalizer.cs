using System;
using System.Collections.Generic;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Restores collection invariants after ink data crosses a JSON persistence boundary.
/// JSON may explicitly contain null collections or null array elements even though
/// the in-memory DTOs initialize those collections by default.
/// </summary>
internal static class InkPayloadNormalizer
{
    internal static InkDocumentData NormalizeDocument(InkDocumentData document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Pages ??= new List<InkPageData>();
        document.Pages.RemoveAll(static page => page is null);
        foreach (var page in document.Pages)
        {
            NormalizePage(page);
        }

        return document;
    }

    internal static InkPageData NormalizePage(InkPageData page)
    {
        ArgumentNullException.ThrowIfNull(page);

        page.Strokes = NormalizeStrokes(page.Strokes);
        return page;
    }

    internal static List<InkStrokeData> NormalizeStrokes(List<InkStrokeData>? strokes)
    {
        if (strokes is null)
        {
            return new List<InkStrokeData>();
        }

        strokes.RemoveAll(static stroke => stroke is null);
        foreach (var stroke in strokes)
        {
            NormalizeStroke(stroke);
        }

        return strokes;
    }

    private static void NormalizeStroke(InkStrokeData stroke)
    {
        stroke.Ribbons ??= new List<InkRibbonData>();
        stroke.Ribbons.RemoveAll(static ribbon => ribbon is null);
        stroke.Blooms ??= new List<InkBloomData>();
        stroke.Blooms.RemoveAll(static bloom => bloom is null);
    }
}
