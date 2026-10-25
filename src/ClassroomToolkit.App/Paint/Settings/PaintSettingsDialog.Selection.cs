using System.Linq;
using ClassroomToolkit.App.Ink;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
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
}
