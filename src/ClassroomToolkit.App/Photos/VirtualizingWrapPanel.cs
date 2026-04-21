using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App.Photos;

public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    private const int CacheRows = 2;
    private WpfSize _extent = WpfSize.Empty;
    private WpfSize _viewport = WpfSize.Empty;
    private WpfPoint _offset;
    private WpfSize _itemSize = WpfSize.Empty;

    public bool CanHorizontallyScroll
    {
        get => false;
        set { }
    }

    public bool CanVerticallyScroll
    {
        get => true;
        set { }
    }

    public double ExtentHeight => _extent.Height;

    public double ExtentWidth => _extent.Width;

    public double HorizontalOffset => 0;

    public double VerticalOffset => _offset.Y;

    public double ViewportHeight => _viewport.Height;

    public double ViewportWidth => _viewport.Width;

    public ScrollViewer? ScrollOwner { get; set; }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        var itemCount = itemsControl?.Items.Count ?? 0;
        if (itemCount == 0)
        {
            CleanupRealizedChildren(0, -1);
            UpdateExtentAndViewport(availableSize, WpfSize.Empty);
            return availableSize;
        }

        EnsureItemSize(availableSize);
        if (!IsItemSizeValid())
        {
            return availableSize;
        }

        var itemsPerRow = CalculateItemsPerRow(availableSize.Width);
        var (firstIndex, lastIndex) = ResolveVisibleRange(itemCount, itemsPerRow, availableSize.Height);

        CleanupRealizedChildren(firstIndex, lastIndex);
        RealizeVisibleChildren(firstIndex, lastIndex, availableSize);

        var extent = new WpfSize(
            CoerceNonNegativeDimension(Math.Max(availableSize.Width, itemsPerRow * _itemSize.Width)),
            CoerceNonNegativeDimension(CalculateExtentHeight(itemCount, itemsPerRow)));
        UpdateExtentAndViewport(availableSize, extent);
        CoerceVerticalOffset();

        return availableSize;
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        if (!IsItemSizeValid())
        {
            return finalSize;
        }

        var itemsPerRow = CalculateItemsPerRow(finalSize.Width);
        var generator = ItemContainerGenerator;
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            var child = InternalChildren[childIndex];
            var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0));
            if (itemIndex < 0)
            {
                continue;
            }

            var row = itemIndex / itemsPerRow;
            var column = itemIndex % itemsPerRow;
            var rect = new Rect(
                column * _itemSize.Width,
                row * _itemSize.Height - _offset.Y,
                _itemSize.Width,
                _itemSize.Height);
            child.Arrange(rect);
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            _offset.Y = 0;
        }

        InvalidateMeasure();
    }

    public void LineUp() => SetVerticalOffset(VerticalOffset - Math.Max(16.0, _itemSize.Height / 3.0));

    public void LineDown() => SetVerticalOffset(VerticalOffset + Math.Max(16.0, _itemSize.Height / 3.0));

    public void LineLeft()
    {
    }

    public void LineRight()
    {
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual is not UIElement element)
        {
            return rectangle;
        }

        var childIndex = InternalChildren.IndexOf(element);
        if (childIndex < 0 || !IsItemSizeValid())
        {
            return rectangle;
        }

        var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0));
        if (itemIndex < 0)
        {
            return rectangle;
        }

        var itemsPerRow = CalculateItemsPerRow(ViewportWidth);
        var row = itemIndex / itemsPerRow;
        var top = row * _itemSize.Height;
        var bottom = top + _itemSize.Height;

        if (top < VerticalOffset)
        {
            SetVerticalOffset(top);
        }
        else if (bottom > VerticalOffset + ViewportHeight)
        {
            SetVerticalOffset(bottom - ViewportHeight);
        }

        return new Rect(0, top, _itemSize.Width, _itemSize.Height);
    }

    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - SystemParameters.WheelScrollLines * 16.0);

    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + SystemParameters.WheelScrollLines * 16.0);

    public void MouseWheelLeft()
    {
    }

    public void MouseWheelRight()
    {
    }

    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);

    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);

    public void PageLeft()
    {
    }

    public void PageRight()
    {
    }

    public void SetHorizontalOffset(double offset)
    {
    }

    public void SetVerticalOffset(double offset)
    {
        var maxOffset = Math.Max(0, ExtentHeight - ViewportHeight);
        var nextOffset = Math.Min(Math.Max(0, offset), maxOffset);
        if (Math.Abs(nextOffset - _offset.Y) < 0.5)
        {
            return;
        }

        _offset.Y = nextOffset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    private void EnsureItemSize(WpfSize availableSize)
    {
        var firstChild = EnsureFirstRealizedChild(availableSize);
        if (firstChild == null)
        {
            return;
        }

        firstChild.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        var desiredSize = firstChild.DesiredSize;
        if (desiredSize.Width > 0 && desiredSize.Height > 0)
        {
            _itemSize = desiredSize;
        }
    }

    private UIElement? EnsureFirstRealizedChild(WpfSize availableSize)
    {
        if (InternalChildren.Count > 0)
        {
            return InternalChildren[0];
        }

        var generator = ItemContainerGenerator;
        var startPosition = generator.GeneratorPositionFromIndex(0);
        using var _ = generator.StartAt(startPosition, GeneratorDirection.Forward, true);
        var child = generator.GenerateNext(out var newlyRealized) as UIElement;
        if (child == null)
        {
            return null;
        }

        if (newlyRealized)
        {
            AddInternalChild(child);
            generator.PrepareItemContainer(child);
        }

        child.Measure(new WpfSize(
            double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width,
            double.PositiveInfinity));
        return child;
    }

    private void RealizeVisibleChildren(int firstIndex, int lastIndex, WpfSize availableSize)
    {
        var generator = ItemContainerGenerator;
        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

        using var _ = generator.StartAt(startPosition, GeneratorDirection.Forward, true);
        for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
        {
            var child = generator.GenerateNext(out var newlyRealized) as UIElement;
            if (child == null)
            {
                continue;
            }

            if (newlyRealized)
            {
                if (childIndex >= InternalChildren.Count)
                {
                    AddInternalChild(child);
                }
                else
                {
                    InsertInternalChild(childIndex, child);
                }

                generator.PrepareItemContainer(child);
            }

            child.Measure(new WpfSize(
                double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width,
                double.PositiveInfinity));
        }
    }

    private void CleanupRealizedChildren(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var position = new GeneratorPosition(childIndex, 0);
            var itemIndex = generator.IndexFromGeneratorPosition(position);
            if (itemIndex < firstIndex || itemIndex > lastIndex)
            {
                generator.Remove(position, 1);
                RemoveInternalChildRange(childIndex, 1);
            }
        }
    }

    private (int FirstIndex, int LastIndex) ResolveVisibleRange(int itemCount, int itemsPerRow, double availableHeight)
    {
        var itemHeight = Math.Max(1.0, _itemSize.Height);
        var startRow = Math.Max(0, (int)Math.Floor(VerticalOffset / itemHeight) - CacheRows);
        var visibleRows = Math.Max(1, (int)Math.Ceiling(Math.Max(availableHeight, itemHeight) / itemHeight) + CacheRows * 2);
        var firstIndex = Math.Max(0, startRow * itemsPerRow);
        var lastIndex = Math.Min(itemCount - 1, ((startRow + visibleRows) * itemsPerRow) - 1);
        return (firstIndex, lastIndex);
    }

    private int CalculateItemsPerRow(double availableWidth)
    {
        if (!IsItemSizeValid())
        {
            return 1;
        }

        var width = double.IsInfinity(availableWidth) || availableWidth <= 0
            ? _itemSize.Width
            : availableWidth;
        return Math.Max(1, (int)Math.Floor(width / Math.Max(1.0, _itemSize.Width)));
    }

    private double CalculateExtentHeight(int itemCount, int itemsPerRow)
    {
        if (!IsItemSizeValid())
        {
            return 0;
        }

        var rows = (int)Math.Ceiling(itemCount / (double)itemsPerRow);
        return rows * _itemSize.Height;
    }

    private bool IsItemSizeValid() => _itemSize.Width > 0 && _itemSize.Height > 0;

    private void UpdateExtentAndViewport(WpfSize availableSize, WpfSize extent)
    {
        var viewport = new WpfSize(
            CoerceNonNegativeDimension(double.IsInfinity(availableSize.Width) ? extent.Width : availableSize.Width),
            CoerceNonNegativeDimension(double.IsInfinity(availableSize.Height) ? extent.Height : availableSize.Height));

        extent = new WpfSize(
            CoerceNonNegativeDimension(extent.Width),
            CoerceNonNegativeDimension(extent.Height));

        var viewportChanged = !AreClose(_viewport, viewport);
        var extentChanged = !AreClose(_extent, extent);

        _viewport = viewport;
        _extent = extent;

        if (viewportChanged || extentChanged)
        {
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    private void CoerceVerticalOffset()
    {
        var maxOffset = Math.Max(0, ExtentHeight - ViewportHeight);
        if (_offset.Y > maxOffset)
        {
            _offset.Y = maxOffset;
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    private static bool AreClose(WpfSize left, WpfSize right)
    {
        return Math.Abs(left.Width - right.Width) < 0.5
            && Math.Abs(left.Height - right.Height) < 0.5;
    }

    private static double CoerceNonNegativeDimension(double value)
    {
        return double.IsNaN(value) || value < 0
            ? 0
            : value;
    }
}
