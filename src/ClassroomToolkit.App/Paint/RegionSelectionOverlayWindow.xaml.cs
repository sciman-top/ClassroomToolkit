using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ClassroomToolkit.App.Paint;

public partial class RegionSelectionOverlayWindow : Window
{
    private readonly DrawingRectangle _virtualBounds;
    private readonly DrawingRectangle[] _passthroughRegions;
    private bool _isSelecting;
    private System.Windows.Point _startPoint;
    private Rect _selectionRect;
    public bool CanceledByPassthrough { get; private set; }
    internal bool SelectionAccepted { get; private set; }
    internal RegionScreenCapturePassthroughInputKind PassthroughInputKind { get; private set; }

    public RegionSelectionOverlayWindow(
        DrawingRectangle virtualBounds,
        IReadOnlyCollection<DrawingRectangle>? passthroughRegions = null)
    {
        InitializeComponent();
        _virtualBounds = virtualBounds;
        _passthroughRegions = passthroughRegions?
            .Where(region => region.Width > 0 && region.Height > 0)
            .ToArray() ?? Array.Empty<DrawingRectangle>();
        Left = virtualBounds.Left;
        Top = virtualBounds.Top;
        Width = Math.Max(virtualBounds.Width, 1);
        Height = Math.Max(virtualBounds.Height, 1);
        Loaded += OnLoaded;
    }

    public bool TryGetSelection(out DrawingRectangle selection)
    {
        if (_selectionRect.Width < 4 || _selectionRect.Height < 4)
        {
            selection = DrawingRectangle.Empty;
            return false;
        }

        var x = (int)Math.Round(Left + _selectionRect.X);
        var y = (int)Math.Round(Top + _selectionRect.Y);
        var width = (int)Math.Round(_selectionRect.Width);
        var height = (int)Math.Round(_selectionRect.Height);
        var raw = new DrawingRectangle(x, y, width, height);
        var normalized = DrawingRectangle.Intersect(_virtualBounds, raw);
        if (normalized.Width < 4 || normalized.Height < 4)
        {
            selection = DrawingRectangle.Empty;
            return false;
        }

        selection = normalized;
        return true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CompleteSelection(accepted: false);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        if (_selectionRect.Width < 4 || _selectionRect.Height < 4)
        {
            CompleteSelection(accepted: false);
        }
        else
        {
            CompleteSelection(accepted: true);
        }
        e.Handled = true;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        if (TryCancelForPassthrough(point, RegionScreenCapturePassthroughInputKind.PointerPress))
        {
            return;
        }

        _startPoint = point;
        _isSelecting = true;
        SelectionRect.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelectionRect(_startPoint, _startPoint);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var currentPoint = e.GetPosition(this);
        if (TryCancelForPassthrough(currentPoint, RegionScreenCapturePassthroughInputKind.PointerMove))
        {
            return;
        }

        if (!_isSelecting)
        {
            return;
        }

        UpdateSelectionRect(_startPoint, currentPoint);
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        ReleaseMouseCapture();
        var endPoint = e.GetPosition(this);
        UpdateSelectionRect(_startPoint, endPoint);
        if (RegionSelectionCompletionPolicy.ResolvePointerRelease(_selectionRect.Width, _selectionRect.Height)
            == RegionSelectionCompletionDecision.KeepWaiting)
        {
            ResetSelectionAndContinue();
        }
        else
        {
            CompleteSelection(accepted: true);
        }
        e.Handled = true;
    }

    private void OnTouchDown(object sender, TouchEventArgs e)
    {
        var point = e.GetTouchPoint(this).Position;
        if (TryCancelForPassthrough(point, RegionScreenCapturePassthroughInputKind.PointerPress))
        {
            e.Handled = true;
            return;
        }

        _startPoint = point;
        _isSelecting = true;
        SelectionRect.Visibility = Visibility.Visible;
        CaptureTouch(e.TouchDevice);
        UpdateSelectionRect(_startPoint, _startPoint);
        e.Handled = true;
    }

    private void OnTouchMove(object sender, TouchEventArgs e)
    {
        var point = e.GetTouchPoint(this).Position;
        if (TryCancelForPassthrough(point, RegionScreenCapturePassthroughInputKind.PointerMove))
        {
            e.Handled = true;
            return;
        }

        if (!_isSelecting)
        {
            return;
        }

        UpdateSelectionRect(_startPoint, point);
        e.Handled = true;
    }

    private void OnTouchUp(object sender, TouchEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        ReleaseTouchCapture(e.TouchDevice);
        var endPoint = e.GetTouchPoint(this).Position;
        UpdateSelectionRect(_startPoint, endPoint);
        if (RegionSelectionCompletionPolicy.ResolvePointerRelease(_selectionRect.Width, _selectionRect.Height)
            == RegionSelectionCompletionDecision.KeepWaiting)
        {
            ResetSelectionAndContinue();
        }
        else
        {
            CompleteSelection(accepted: true);
        }
        e.Handled = true;
    }

    private bool TryCancelForPassthrough(
        System.Windows.Point _,
        RegionScreenCapturePassthroughInputKind inputKind)
    {
        if (_isSelecting || _passthroughRegions.Length == 0)
        {
            return false;
        }

        var cursorPosition = System.Windows.Forms.Cursor.Position;
        var x = cursorPosition.X;
        var y = cursorPosition.Y;
        for (var i = 0; i < _passthroughRegions.Length; i++)
        {
            if (!_passthroughRegions[i].Contains(x, y))
            {
                continue;
            }

            CanceledByPassthrough = true;
            PassthroughInputKind = inputKind;
            Cursor = System.Windows.Input.Cursors.Arrow;
            CompleteSelection(accepted: false);
            return true;
        }

        return false;
    }

    internal bool CancelFromToolbarHandledPress()
    {
        if (!IsVisible)
        {
            return false;
        }

        CanceledByPassthrough = true;
        PassthroughInputKind = RegionScreenCapturePassthroughInputKind.ToolbarHandledPress;
        Cursor = System.Windows.Input.Cursors.Arrow;
        CompleteSelection(accepted: false);
        return true;
    }

    internal bool CancelFromToolbarPointerMove()
    {
        if (!IsVisible)
        {
            return false;
        }

        CanceledByPassthrough = true;
        PassthroughInputKind = RegionScreenCapturePassthroughInputKind.PointerMove;
        Cursor = System.Windows.Input.Cursors.Arrow;
        CompleteSelection(accepted: false);
        return true;
    }

    private void UpdateSelectionRect(System.Windows.Point start, System.Windows.Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        _selectionRect = new Rect(x, y, width, height);
        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = width;
        SelectionRect.Height = height;
    }

    private void ResetSelectionAndContinue()
    {
        _selectionRect = Rect.Empty;
        SelectionRect.Visibility = Visibility.Collapsed;
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
    }

    private void CompleteSelection(bool accepted)
    {
        SelectionAccepted = accepted;
        Close();
    }
}
