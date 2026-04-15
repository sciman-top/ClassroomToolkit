# UI Best Practice End State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade ClassroomToolkit to a unified, compact, performance-safe `精致现代化` UI end state by standardizing shared theme resources and polishing the highest-visibility windows first.

**Architecture:** Land shared theme-token and control-style changes before window-specific markup changes so the visual language is defined once and reused everywhere. Keep implementation concentrated in `App.xaml`, style dictionaries, window XAML, and existing UI contract tests, with incremental checkpoints after each style family or window group.

**Tech Stack:** WPF (.NET 10), XAML resource dictionaries, code-behind/ViewModel bindings, xUnit UI contract tests, PowerShell validation scripts

---

## File Structure

### Shared theme and shell resources

- Modify: `src/ClassroomToolkit.App/App.xaml`
  - application-level merged dictionaries and global defaults
- Modify: `src/ClassroomToolkit.App/Assets/Styles/Colors.xaml`
  - semantic colors, brushes, gradients, shadows
- Modify: `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`
  - density tokens, typography tiers, shell styles, shared control templates
- Modify: `src/ClassroomToolkit.App/Assets/Styles/Icons.xaml`
  - icon geometry cleanup only if visual weight or alignment needs normalization

### Core windows

- Modify: `src/ClassroomToolkit.App/MainWindow.xaml`
- Modify: `src/ClassroomToolkit.App/RollCallWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml`

### Secondary normalization pass

- Modify: `src/ClassroomToolkit.App/AboutDialog.xaml`
- Modify: `src/ClassroomToolkit.App/AutoExitDialog.xaml`
- Modify: `src/ClassroomToolkit.App/ClassSelectDialog.xaml`
- Modify: `src/ClassroomToolkit.App/RemoteKeyDialog.xaml`
- Modify: `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/StudentListDialog.xaml`
- Modify: `src/ClassroomToolkit.App/TimerSetDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/BoardColorDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml`
- Modify: `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml`

### Tests and validation targets

- Modify: `tests/ClassroomToolkit.Tests/App/ThemeResourceDictionaryTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetStylesContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetShellSizeContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetShellSpacingRadiusContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetTypographyContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
- Verify: `tests/ClassroomToolkit.Tests/App/TimerSetDialogXamlContractTests.cs`

## Task 1: Lock shared visual tokens and contract expectations

**Files:**
- Modify: `src/ClassroomToolkit.App/Assets/Styles/Colors.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/ThemeResourceDictionaryTests.cs`

- [ ] **Step 1: Add or tighten failing token-contract assertions**

```csharp
[Fact]
public void ColorsDictionary_ShouldExposeRefinedUiEndStateTokens()
{
    var dictionary = LoadColorsDictionary();

    var requiredKeys = new[]
    {
        "Brush_AppBackground",
        "Brush_Surface_Primary",
        "Brush_Surface_Secondary",
        "Brush_InputBackground",
        "Brush_Border_Subtle",
        "Brush_Border_Strong",
        "Brush_Border_Focus",
        "Gradient_Primary_Subtle",
        "Gradient_Teaching_Subtle",
        "Shadow_Dialog",
        "Shadow_Floating"
    };

    foreach (var key in requiredKeys)
    {
        dictionary.Contains(key).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run token tests to confirm the new assertion shape**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ThemeResourceDictionaryTests"`

Expected: existing tests pass, or any new assertion you added fails only on the missing/incorrect token contract you intend to implement.

- [ ] **Step 3: Refine shared colors, gradients, and shadow weights in `Colors.xaml`**

```xaml
<Color x:Key="Color_Bg_App">#0B1014</Color>
<Color x:Key="Color_Bg_Surface_1">#141B22</Color>
<Color x:Key="Color_Bg_Surface_2">#1A232B</Color>
<Color x:Key="Color_Border_Subtle">#2D3942</Color>
<Color x:Key="Color_Border_Strong">#41505C</Color>
<Color x:Key="Color_Accent_Primary">#4DA6CB</Color>
<Color x:Key="Color_Accent_Teaching">#6AA184</Color>
```

Keep legacy aliases intact. Reduce accent competition rather than adding new visual effects.

- [ ] **Step 4: Re-run token tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ThemeResourceDictionaryTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Assets/Styles/Colors.xaml tests/ClassroomToolkit.Tests/App/ThemeResourceDictionaryTests.cs
git commit -m "refactor: tighten shared UI color tokens"
```

## Task 2: Normalize shared density, typography, and shell styles

**Files:**
- Modify: `src/ClassroomToolkit.App/App.xaml`
- Modify: `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetStylesContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetShellSizeContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetShellSpacingRadiusContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WidgetTypographyContractTests.cs`

- [ ] **Step 1: Extend style-contract tests for the refined compact shell system**

```csharp
[Fact]
public void WidgetStyles_ShouldExposeCompactShellDensityTokens()
{
    var xaml = File.ReadAllText(GetWidgetStylesPath());

    xaml.Should().Contain("Size_Button_Icon_Compact");
    xaml.Should().Contain("Size_Shell_TitleBar_Dialog");
    xaml.Should().Contain("FontSize_Title_Dialog");
    xaml.Should().Contain("Radius_Shell_Dialog");
}
```

- [ ] **Step 2: Run the shared-widget contract subset**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WidgetStylesContractTests|FullyQualifiedName~WidgetShellSizeContractTests|FullyQualifiedName~WidgetShellSpacingRadiusContractTests|FullyQualifiedName~WidgetTypographyContractTests"`

Expected: FAIL only where the new compact-system assertions are not yet satisfied.

- [ ] **Step 3: Update global defaults and shared control templates**

```xaml
<Style TargetType="Window">
    <Setter Property="TextOptions.TextFormattingMode" Value="Display"/>
    <Setter Property="UseLayoutRounding" Value="True"/>
    <Setter Property="FontFamily" Value="Microsoft YaHei UI"/>
</Style>

<sys:Double x:Key="Size_Button_Icon_Compact">28</sys:Double>
<sys:Double x:Key="Size_Button_Action_Height_Compact">30</sys:Double>
<sys:Double x:Key="FontSize_Body_M">13</sys:Double>
<sys:Double x:Key="FontSize_Title_Dialog">14</sys:Double>
```

Adjust `Style_PrimaryButton`, `Style_SecondaryButton`, `Style_DangerButton`, `Style_IconButton`, shell title bars, dialog footer bars, tabs, menu items, list items, and helper-text styling to use the same density and typography rules.

- [ ] **Step 4: Re-run the shared-widget contract subset**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WidgetStylesContractTests|FullyQualifiedName~WidgetShellSizeContractTests|FullyQualifiedName~WidgetShellSpacingRadiusContractTests|FullyQualifiedName~WidgetTypographyContractTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/App.xaml src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml tests/ClassroomToolkit.Tests/App/WidgetStylesContractTests.cs tests/ClassroomToolkit.Tests/App/WidgetShellSizeContractTests.cs tests/ClassroomToolkit.Tests/App/WidgetShellSpacingRadiusContractTests.cs tests/ClassroomToolkit.Tests/App/WidgetTypographyContractTests.cs
git commit -m "refactor: unify compact shell and control styles"
```

## Checkpoint A: Shared theme baseline

- [ ] Build succeeds: `dotnet build ClassroomToolkit.sln -c Debug`
- [ ] Shared style tests pass: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ThemeResourceDictionaryTests|FullyQualifiedName~WidgetStylesContractTests|FullyQualifiedName~WidgetShellSizeContractTests|FullyQualifiedName~WidgetShellSpacingRadiusContractTests|FullyQualifiedName~WidgetTypographyContractTests"`
- [ ] Manually inspect affected shared controls in at least one work shell, one dialog shell, and one management shell before continuing

## Task 3: Polish the main teaching shell windows

**Files:**
- Modify: `src/ClassroomToolkit.App/MainWindow.xaml`
- Modify: `src/ClassroomToolkit.App/RollCallWindow.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`

- [ ] **Step 1: Tighten copy expectations for the primary teaching windows**

```csharp
[Fact]
public void MainWindow_ShouldUseCompactPrimaryLabels()
{
    var xaml = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

    xaml.Should().Contain("Content=\"点名与计时\"");
    xaml.Should().NotContain("Content=\"点名 / 计时\"");
}
```

If `RollCallWindow` helper copy or button labels need updates, add exact assertions in the same test file before markup changes.

- [ ] **Step 2: Run the copy contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: FAIL only for the new teaching-window assertions you added.

- [ ] **Step 3: Update `MainWindow.xaml` and `RollCallWindow.xaml` to the refined shell**

```xaml
<Button x:Name="PaintButton"
        Style="{StaticResource Style_WorkShellHeroTileButton}"
        Content="画笔"
        Tag="{StaticResource Icon_Pen}"/>

<Button x:Name="RollCallButton"
        Style="{StaticResource Style_WorkShellHeroTileButton}"
        Content="点名与计时"
        Tag="{StaticResource Icon_RollCall}"/>
```

Apply the shared compact rhythm to:

- title/header spacing
- hero action emphasis
- bottom utility grouping
- roll-call center content hierarchy
- timer display framing
- roll-call action strip spacing and button consistency

- [ ] **Step 4: Re-run copy contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: PASS for the teaching-window assertions

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/MainWindow.xaml src/ClassroomToolkit.App/RollCallWindow.xaml tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: polish main teaching shell windows"
```

## Task 4: Refine the floating paint surfaces

**Files:**
- Modify: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/BoardColorDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`

- [ ] **Step 1: Add copy or shell assertions for floating paint windows if needed**

```csharp
[Fact]
public void FloatingAndManagementWindows_ShouldUseCompactLabels()
{
    var paletteXaml = File.ReadAllText(GetXamlPath("Paint", "QuickColorPaletteWindow.xaml"));

    paletteXaml.Should().Contain("Title=\"快捷颜色\"");
    paletteXaml.Should().Contain("Text=\"颜色\"");
}
```

- [ ] **Step 2: Run the copy contract tests again**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: FAIL only on the new floating-window assertions you added.

- [ ] **Step 3: Recompose the floating paint windows around compact grouped controls**

```xaml
<Border x:Name="ToolbarContainer"
        Background="{StaticResource Brush_Surface_Primary}"
        BorderBrush="{StaticResource Brush_Border_Subtle}"
        CornerRadius="20"
        Padding="8,4">
```

Apply:

- tighter group rhythm in `PaintToolbarWindow`
- clearer active/neutral/destructive tool states
- lower visual weight on non-content overlay chrome in `PaintOverlayWindow`
- dialog-family normalization for `BoardColorDialog`
- compact palette presentation in `QuickColorPaletteWindow`

- [ ] **Step 4: Re-run copy contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: PASS for floating paint windows

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml src/ClassroomToolkit.App/Paint/BoardColorDialog.xaml src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: refine floating paint window visuals"
```

## Task 5: Rework the high-density settings dialogs

**Files:**
- Modify: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/TimerSetDialog.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
- Verify: `tests/ClassroomToolkit.Tests/App/TimerSetDialogXamlContractTests.cs`

- [ ] **Step 1: Add or update exact copy assertions for settings surfaces**

```csharp
paintXaml.Should().Contain("Header=\"基础\"");
paintXaml.Should().Contain("Content=\"全部重置\"");
rollCallXaml.Should().Contain("Header=\"显示\"");
rollCallXaml.Should().Contain("Header=\"语音\"");
inkSettingsXaml.Should().Contain("Title=\"笔迹记录\"");
```

- [ ] **Step 2: Run the settings-copy and timer contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests|FullyQualifiedName~TimerSetDialogXamlContractTests"`

Expected: FAIL only on the new copy or layout expectations you added.

- [ ] **Step 3: Normalize tabs, field rows, helper text, and footer actions**

```xaml
<TabItem Header="基础">
    <StackPanel Margin="0,12,0,0">
        <TextBlock Text="先选预设，再调参数。"
                   Foreground="{StaticResource Brush_Text_Tertiary}"
                   FontSize="12"/>
    </StackPanel>
</TabItem>
```

Apply:

- consistent field-label widths
- shorter helper text
- clearer standard-vs-advanced section boundaries
- normalized footer button order and action emphasis

- [ ] **Step 4: Re-run settings-copy and timer contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests|FullyQualifiedName~TimerSetDialogXamlContractTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml src/ClassroomToolkit.App/RollCallSettingsDialog.xaml src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml src/ClassroomToolkit.App/TimerSetDialog.xaml tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: polish settings dialog density and copy"
```

## Checkpoint B: Teaching + paint + settings surfaces

- [ ] Build succeeds: `dotnet build ClassroomToolkit.sln -c Debug`
- [ ] Focused test subset passes: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests|FullyQualifiedName~TimerSetDialogXamlContractTests|FullyQualifiedName~ThemeResourceDictionaryTests|FullyQualifiedName~WidgetStylesContractTests|FullyQualifiedName~WidgetShellSizeContractTests|FullyQualifiedName~WidgetShellSpacingRadiusContractTests|FullyQualifiedName~WidgetTypographyContractTests"`
- [ ] Manually review the main entry, toolbar, paint settings, and roll-call window before entering management-window work

## Task 6: Polish the management window and fullscreen content windows

**Files:**
- Modify: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`

- [ ] **Step 1: Extend management-window copy assertions if needed**

```csharp
imageManagerXaml.Should().Contain("Text=\"先选左侧文件夹\"");
imageManagerXaml.Should().Contain("ToolTip=\"输入后回车\"");
photoOverlayXaml.Should().Contain("Text=\"点击空白关闭\"");
groupOverlayXaml.Should().Contain("Title=\"分组提示\"");
```

- [ ] **Step 2: Run the copy contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: FAIL only on the new management/fullscreen assertions you added.

- [ ] **Step 3: Recompose `ImageManagerWindow.xaml` and fullscreen overlays**

```xaml
<Grid Grid.Row="0" Style="{StaticResource Style_ManagementShellTitleBar}">
    <StackPanel Orientation="Horizontal" Margin="20,0">
        <TextBlock Text="资源管理" Style="{StaticResource Style_ManagementShellTitleText}"/>
        <TextBlock Text="文件夹、PDF、图片" Style="{StaticResource Style_ManagementShellSubtitleText}"/>
    </StackPanel>
</Grid>
```

Apply:

- calmer left/right pane visual relationship
- tighter management toolbar and address bar composition
- more restrained overlay controls in fullscreen windows
- stronger content-first balance in photo/group overlays

- [ ] **Step 4: Re-run copy contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: polish management and fullscreen windows"
```

## Task 7: Normalize the remaining dialogs and utility surfaces

**Files:**
- Modify: `src/ClassroomToolkit.App/AboutDialog.xaml`
- Modify: `src/ClassroomToolkit.App/AutoExitDialog.xaml`
- Modify: `src/ClassroomToolkit.App/ClassSelectDialog.xaml`
- Modify: `src/ClassroomToolkit.App/RemoteKeyDialog.xaml`
- Modify: `src/ClassroomToolkit.App/StudentListDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
- Modify: `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`
- Modify: `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`

- [ ] **Step 1: Confirm exact copy expectations for the remaining dialogs**

```csharp
aboutXaml.Should().Contain("Text=\"课堂常用工具\"");
autoExitXaml.Should().Contain("Text=\"0 表示不自动关闭。\"");
diagnosticsXaml.Should().Contain("Text=\"兼容诊断\"");
remoteKeyXaml.Should().Contain("Title=\"遥控键\"");
studentListXaml.Should().Contain("Title=\"班级名单\"");
```

- [ ] **Step 2: Run the copy contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: FAIL only on the remaining-dialog assertions you added.

- [ ] **Step 3: Move each remaining dialog to the standardized dialog shell language**

```xaml
<Border Style="{StaticResource Style_DialogShellWindowBorder}">
    <Grid>
        <Grid Grid.Row="0" Style="{StaticResource Style_DialogShellTitleBar}">
            <TextBlock Style="{StaticResource Style_DialogShellTitleText}" Text="关于软件"/>
        </Grid>
    </Grid>
</Border>
```

Normalize:

- title bar layout
- close button position and tooltips
- footer action hierarchy
- helper text style
- compact spacing and card treatment

- [ ] **Step 4: Re-run copy contract tests**

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~UiCopyContractTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/AboutDialog.xaml src/ClassroomToolkit.App/AutoExitDialog.xaml src/ClassroomToolkit.App/ClassSelectDialog.xaml src/ClassroomToolkit.App/RemoteKeyDialog.xaml src/ClassroomToolkit.App/StudentListDialog.xaml src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml src/ClassroomToolkit.App/LauncherBubbleWindow.xaml tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: normalize utility dialogs and prompts"
```

## Checkpoint C: Full UI scope

- [ ] Build succeeds: `dotnet build ClassroomToolkit.sln -c Debug`
- [ ] Full test suite passes: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- [ ] Contract/invariant subset passes: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- [ ] Hotspot check passes: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Task 8: Final visual QA and cleanup

**Files:**
- Modify: any XAML/style/test files still needed after final QA

- [ ] **Step 1: Run a final visual sweep across all shell families**

```text
Check work shell, management shell, dialog shell, and fullscreen shell for:
- inconsistent padding
- overstated accent colors
- icon misalignment
- helper text that is still too long
- controls that look compact but have poor hit targets
```

- [ ] **Step 2: Apply only targeted cleanup edits**

```xaml
<TextBlock Foreground="{StaticResource Brush_Text_Tertiary}"
           FontSize="12"
           TextWrapping="Wrap"/>
```

Keep cleanup focused. Do not start new redesign work in this phase.

- [ ] **Step 3: Re-run the full validation chain**

Run: `dotnet build ClassroomToolkit.sln -c Debug`

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

Run: `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

Expected: all commands PASS

- [ ] **Step 4: Commit**

```bash
git add src/ClassroomToolkit.App tests/ClassroomToolkit.Tests
git commit -m "refactor: finish UI best practice end state"
```

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Shared style change causes unintended window regressions | High | Land shell/style changes before window sweeps and checkpoint after each shell family |
| Compacting controls reduces usability | High | Keep hit targets safe even when visuals tighten; manually inspect high-frequency controls |
| Copy cleanup changes feature meaning | Medium | Gate every ambiguous rename through code-behind/binding/test verification |
| Heavy effects hurt overlay performance | High | Prefer static resources and restrained shadows; avoid animation-heavy polish |
| Scope drifts into behavioral redesign | Medium | Keep tasks XAML/style/test focused and use checkpoints to stop expansion |

## Self-Review

### Spec coverage

- Shared token unification is covered by Tasks 1-2.
- Core teaching, paint, settings, management, fullscreen, and remaining dialogs are covered by Tasks 3-7.
- Performance-safe polish and final validation are covered by Task 8 and Checkpoints A-C.

### Placeholder scan

- No `TBD`, `TODO`, or deferred "implement later" markers remain.
- Every task includes explicit files, commands, and expected outcomes.

### Type and naming consistency

- Plan references existing file paths and existing test classes found in the repository.
- Shared token/style names match current resource naming patterns such as `Brush_*`, `Style_*`, and `Gradient_*`.
