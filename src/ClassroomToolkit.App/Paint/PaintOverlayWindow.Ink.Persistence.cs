using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void MarkCurrentInkPageLoaded(IReadOnlyList<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath) || _currentPageIndex <= 0)
        {
            return;
        }

        MarkInkPageLoaded(_currentDocumentPath, _currentPageIndex, strokes);
    }

    private void MarkInkPageLoaded(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        _inkDirtyPages.MarkLoaded(sourcePath, pageIndex, ComputeInkHash(strokes));
        if (IsCurrentInkPage(sourcePath, pageIndex))
        {
            SyncSessionInkDirtyFlag();
        }

        ClearInkWalSnapshot(sourcePath, pageIndex);
    }

    private void MarkCurrentInkPageModified()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath) || _currentPageIndex <= 0)
        {
            return;
        }

        var hash = ComputeInkHash(_inkStrokes);
        _inkDirtyPages.MarkModified(_currentDocumentPath, _currentPageIndex, hash);
        TrackInkWalSnapshot(_currentDocumentPath, _currentPageIndex, _inkStrokes, hash);
        SyncSessionInkDirtyFlag();
    }

    private bool MarkInkPagePersistedIfUnchanged(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        var persisted = _inkDirtyPages.MarkPersistedIfUnchanged(sourcePath, pageIndex, hash);
        if (persisted)
        {
            ClearInkWalSnapshot(sourcePath, pageIndex);
        }

        if (IsCurrentInkPage(sourcePath, pageIndex))
        {
            SyncSessionInkDirtyFlag();
        }

        return persisted;
    }

    private void MarkInkPageModified(string sourcePath, int pageIndex, string hash, IReadOnlyList<InkStrokeData>? walSnapshot = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        _inkDirtyPages.MarkModified(sourcePath, pageIndex, hash);
        if (walSnapshot != null)
        {
            TrackInkWalSnapshot(sourcePath, pageIndex, walSnapshot, hash);
        }

        if (IsCurrentInkPage(sourcePath, pageIndex))
        {
            SyncSessionInkDirtyFlag();
        }
    }

    private bool IsCurrentInkPage(string sourcePath, int pageIndex)
    {
        return pageIndex == _currentPageIndex
            && !string.IsNullOrWhiteSpace(_currentDocumentPath)
            && string.Equals(sourcePath, _currentDocumentPath, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncSessionInkDirtyFlag()
    {
        var isDirty = IsCurrentPageDirty();
        var current = _sessionCoordinator.CurrentState;
        if (current.InkDirty == isDirty)
        {
            return;
        }

        DispatchSessionEvent(isDirty
            ? new MarkInkDirtyEvent()
            : new MarkInkSavedEvent());
    }

    private void TrackInkWalSnapshot(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes, string hash)
    {
        if (!InkPersistenceTogglePolicy.ShouldTrackWal(_inkSaveEnabled))
        {
            return;
        }

        _ = SafeActionExecutionExecutor.TryExecute(
            () => _inkWal.Upsert(sourcePath, pageIndex, CloneInkStrokes(strokes), hash));
    }

    private void ClearInkWalSnapshot(string sourcePath, int pageIndex)
    {
        _ = SafeActionExecutionExecutor.TryExecute(
            () => _inkWal.Remove(sourcePath, pageIndex));
    }

    private void RecoverInkWalForDirectory(string sourcePath)
    {
        if (!InkPersistenceTogglePolicy.ShouldRecoverWal(_inkSaveEnabled)
            || string.IsNullOrWhiteSpace(sourcePath)
            || _inkPersistence == null)
        {
            return;
        }

        _ = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                var directoryPath = System.IO.Path.GetDirectoryName(sourcePath);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    return;
                }

                var recovered = _inkWal.RecoverDirectory(directoryPath, _inkPersistence, ComputeInkHash);
                if (recovered > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[InkWAL] Recovered {recovered} pending pages in {directoryPath}");
                }
            },
            ex => System.Diagnostics.Debug.WriteLine($"[InkWAL] Recover failed: {ex.Message}"));
    }

    private bool WasPageModifiedInSession(string sourcePath, int pageIndex)
    {
        return _inkDirtyPages.WasModifiedInSession(sourcePath, pageIndex);
    }

    private IEnumerable<string> EnumerateSessionModifiedSourcesInDirectory(string directoryPath)
    {
        return _inkDirtyPages.EnumerateSessionModifiedSourcesInDirectory(directoryPath);
    }

    private static string ComputeInkHash(IReadOnlyList<InkStrokeData> strokes)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return "empty";
        }

        var builder = new StringBuilder(strokes.Count * 64);
        foreach (var stroke in strokes)
        {
            builder.Append(stroke.Type).Append('|')
                .Append(stroke.BrushStyle).Append('|')
                .Append(stroke.ColorHex).Append('|')
                .Append(stroke.Opacity).Append('|')
                .Append(stroke.BrushSize.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.ReferenceWidth.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.ReferenceHeight.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.GeometryPath ?? string.Empty).Append('|')
                .Append(stroke.Ribbons.Count).Append('|');
            foreach (var ribbon in stroke.Ribbons)
            {
                builder.Append(ribbon.GeometryPath ?? string.Empty).Append('@')
                    .Append(ribbon.Opacity.ToString("G17", CultureInfo.InvariantCulture)).Append('@')
                    .Append(ribbon.RibbonT.ToString("G17", CultureInfo.InvariantCulture)).Append(';');
            }

            builder.Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
