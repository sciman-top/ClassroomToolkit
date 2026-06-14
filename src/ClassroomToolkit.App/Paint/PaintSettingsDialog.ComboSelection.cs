using System.Linq;
using ClassroomToolkit.App.Settings;
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

    private static double FindNearestScale(double value)
    {
        var target = Clamp(value, ToolbarScaleDefaults.Min, ToolbarScaleDefaults.Max);
        return ToolbarScaleChoices.OrderBy(choice => Math.Abs(choice - target)).First();
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
