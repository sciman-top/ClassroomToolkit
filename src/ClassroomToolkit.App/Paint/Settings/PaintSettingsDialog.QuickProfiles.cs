using System.Collections.Generic;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void UpdateChangeSummaryText()
    {
        if (ChangeSummaryText == null)
        {
            return;
        }

        var changedSections = new List<string>(3);
        if (IsPresetBrushSectionDirty())
        {
            changedSections.Add("基础");
        }
        if (IsAdvancedSectionDirty())
        {
            changedSections.Add("工具栏");
        }
        if (IsSceneSectionDirty())
        {
            changedSections.Add("兼容");
        }

        ChangeSummaryText.Text = changedSections.Count == 0
            ? "本次未修改任何设置。"
            : $"本次已修改：{string.Join("、", changedSections)}。";
    }

}
