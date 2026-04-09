using System.Linq;
using System.Windows;
using ClassroomToolkit.App.Ink;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private static int ToPercent(byte value)
    {
        return (int)Math.Round(value * PaintSettingsDefaults.PercentMax / PaintSettingsDefaults.PercentToByteScale);
    }

    private static byte ToByte(double percent)
    {
        var clamped = Math.Max(PaintSettingsDefaults.PercentMin, Math.Min(PaintSettingsDefaults.PercentMax, percent));
        return (byte)Math.Clamp(
            (int)Math.Round(clamped * PaintSettingsDefaults.PercentToByteScale / PaintSettingsDefaults.PercentMax),
            0,
            255);
    }

    private void SelectShapeType(PaintShapeType type)
    {
        foreach (var item in ShapeCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is PaintShapeType tagged && tagged == type)
            {
                ShapeCombo.SelectedItem = item;
                return;
            }
        }
        ShapeCombo.SelectedIndex = 0;
    }

    private PaintShapeType ResolveShapeType()
    {
        if (ShapeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is PaintShapeType type)
        {
            return type;
        }
        return PaintShapeType.None;
    }

    private static string GetSelectedTag(WpfComboBox combo, string fallback)
    {
        if (combo.SelectedItem is WpfComboBoxItem item && item.Tag is string text)
        {
            return text;
        }
        return fallback;
    }

    private static void SelectComboByTag(WpfComboBox combo, string value, string fallback)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if ((item.Tag as string ?? string.Empty) == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if ((item.Tag as string ?? string.Empty) == fallback)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static void SelectComboByTag(WpfComboBox combo, double value)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tag && Math.Abs(tag - value) < PaintSettingsDefaults.ComboTagComparisonEpsilon)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private void SelectBrushStyle(PaintBrushStyle style)
    {
        foreach (var item in BrushStyleCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is PaintBrushStyle tagged && tagged == style)
            {
                BrushStyleCombo.SelectedItem = item;
                return;
            }
        }
        BrushStyleCombo.SelectedIndex = 0;
    }

    private PaintBrushStyle ResolveBrushStyle()
    {
        if (BrushStyleCombo.SelectedItem is WpfComboBoxItem item && item.Tag is PaintBrushStyle style)
        {
            return style;
        }
        return PaintBrushStyle.StandardRibbon;
    }

    private void SelectWhiteboardPreset(WhiteboardBrushPreset preset)
    {
        foreach (var item in WhiteboardPresetCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is WhiteboardBrushPreset tagged && tagged == preset)
            {
                WhiteboardPresetCombo.SelectedItem = item;
                return;
            }
        }
        WhiteboardPresetCombo.SelectedIndex = 0;
    }

    private void SelectCalligraphyPreset(CalligraphyBrushPreset preset)
    {
        foreach (var item in CalligraphyPresetCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is CalligraphyBrushPreset tagged && tagged == preset)
            {
                CalligraphyPresetCombo.SelectedItem = item;
                return;
            }
        }
        CalligraphyPresetCombo.SelectedIndex = 0;
    }

    private WhiteboardBrushPreset ResolveWhiteboardPreset()
    {
        if (WhiteboardPresetCombo.SelectedItem is WpfComboBoxItem item && item.Tag is WhiteboardBrushPreset preset)
        {
            return preset;
        }
        return WhiteboardBrushPreset.Smooth;
    }

    private CalligraphyBrushPreset ResolveCalligraphyPreset()
    {
        if (CalligraphyPresetCombo.SelectedItem is WpfComboBoxItem item && item.Tag is CalligraphyBrushPreset preset)
        {
            return preset;
        }
        return CalligraphyBrushPreset.Sharp;
    }

    private void SelectClassroomWritingMode(ClassroomWritingMode mode)
    {
        foreach (var item in ClassroomWritingModeCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is ClassroomWritingMode tagged && tagged == mode)
            {
                ClassroomWritingModeCombo.SelectedItem = item;
                return;
            }
        }
        ClassroomWritingModeCombo.SelectedIndex = 1;
    }

    private ClassroomWritingMode ResolveClassroomWritingMode()
    {
        if (ClassroomWritingModeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is ClassroomWritingMode mode)
        {
            return mode;
        }
        return ClassroomWritingMode.Balanced;
    }

    private static double FindNearestScale(double value)
    {
        var target = Clamp(value, ToolbarScaleDefaults.Min, ToolbarScaleDefaults.Max);
        return ToolbarScaleChoices.OrderBy(choice => Math.Abs(choice - target)).First();
    }

    private double GetSelectedScale()
    {
        if (ToolbarScaleCombo.SelectedItem is WpfComboBoxItem item && item.Tag is double scale)
        {
            return scale;
        }
        return 1.0;
    }

    private void SelectInkExportScope(InkExportScope scope)
    {
        foreach (var item in InkExportScopeCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is InkExportScope tagged && tagged == scope)
            {
                InkExportScopeCombo.SelectedItem = item;
                return;
            }
        }
        InkExportScopeCombo.SelectedIndex = 0;
    }

    private InkExportScope ResolveInkExportScope()
    {
        if (InkExportScopeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is InkExportScope scope)
        {
            return scope;
        }
        return InkExportScope.AllPersistedAndSession;
    }

    private static void SelectIntCombo(WpfComboBox combo, int value, int fallback)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is int tagged && tagged == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is int tagged && tagged == fallback)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static void EnsureIntComboOption(WpfComboBox combo, int value, string label)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is int tagged && tagged == value)
            {
                return;
            }
        }

        combo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
    }

    private static int ResolveIntCombo(WpfComboBox combo, int fallback)
    {
        if (combo.SelectedItem is WpfComboBoxItem item && item.Tag is int value)
        {
            return value;
        }
        return fallback;
    }

    private static void SelectDoubleCombo(WpfComboBox combo, double value, double fallback)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tagged && Math.Abs(tagged - value) < PaintSettingsDefaults.DoubleComparisonEpsilon)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tagged && Math.Abs(tagged - fallback) < PaintSettingsDefaults.DoubleComparisonEpsilon)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static void EnsureDoubleComboOption(WpfComboBox combo, double value, string label)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tagged
                && Math.Abs(tagged - value) < PaintSettingsDefaults.DoubleComparisonEpsilon)
            {
                return;
            }
        }

        combo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
    }

    private static double ResolveDoubleCombo(WpfComboBox combo, double fallback)
    {
        if (combo.SelectedItem is WpfComboBoxItem item && item.Tag is double value)
        {
            return value;
        }
        return fallback;
    }
}
