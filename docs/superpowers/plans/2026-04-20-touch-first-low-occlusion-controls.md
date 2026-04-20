# Touch-First Low-Occlusion Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a touch-first, low-occlusion control model for classroom all-in-one devices: compact resting UI, tap-first primary paths, larger hit targets, explicit access to common settings, and lower input cost across high-frequency flows.

**Architecture:** Build a shared touch baseline first, then migrate high-frequency surfaces to a repeat-tap local-settings pattern or a single shared overflow. Keep behavior decisions in small policy/helper files where possible so xUnit can cover the logic, and keep WPF partial windows focused on event wiring and view updates.

**Tech Stack:** .NET 10, WPF, XAML resource dictionaries, xUnit, FluentAssertions, existing partial-window structure under `src/ClassroomToolkit.App`

---

## File Map

### New files

- `src/ClassroomToolkit.App/Paint/ToolbarSecondTapTarget.cs`
  - Enumerates toolbar surfaces that support second-tap local settings.
- `src/ClassroomToolkit.App/Paint/ToolbarSecondTapIntentPolicy.cs`
  - Resolves whether a pre-click pointer-down should be treated as a normal activation or a second-tap secondary-action request.
- `src/ClassroomToolkit.App/Paint/BoardPrimaryAction.cs`
  - Stores the last-used explicit board primary action (`CaptureRegion` or `EnterWhiteboard`).
- `tests/ClassroomToolkit.Tests/App/TouchFirstMetricsXamlContractTests.cs`
  - Guards shared touch target tokens and scroll target sizing.
- `tests/ClassroomToolkit.Tests/LongPressBehaviorContractTests.cs`
  - Guards touch-aware long-press event registration in the shared behavior.
- `tests/ClassroomToolkit.Tests/ToolbarSecondTapIntentPolicyTests.cs`
  - Covers repeat-tap secondary-action decisions.
- `tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs`
  - Guards toolbar XAML and code-behind hookups for compact local settings access.
- `tests/ClassroomToolkit.Tests/BoardPrimaryActionTests.cs`
  - Covers board default-action memory and explicit panel behavior.
- `tests/ClassroomToolkit.Tests/LauncherBubbleTouchContractTests.cs`
  - Guards touch handlers and the small-visual / larger-hit-region bubble contract.
- `tests/ClassroomToolkit.Tests/App/LauncherOverflowContractTests.cs`
  - Guards the shared launcher overflow menu for low-frequency settings.
- `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`
  - Guards single-tap open and explicit multi-select entry in the image manager.
- `tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs`
  - Guards timer touch repeat, auto-exit focus behavior, and photo overlay close affordance.
- `docs/change-evidence/20260420-touch-first-low-occlusion-controls-implementation.md`
  - Stores final implementation evidence, gate output, hotspot review, and rollback notes.

### Modified files

- `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`
  - Introduce touch-size tokens and raise compact-control minimum hit targets without forcing large visual ornaments everywhere.
- `src/ClassroomToolkit.App/Behaviors/LongPressBehavior.cs`
  - Add touch-aware hold wiring so hold remains an accelerator on touch devices, not a mouse-only path.
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
  - Hook repeat-tap pointer-down handlers, add compact local popovers, and replace hidden-only toolbar setting paths.
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - Store second-tap intent, branch between activation and local settings, and host explicit board actions.
- `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml`
  - Keep the palette compact but touchable.
- `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml.cs`
  - Raise actual color target sizes and reuse shared touch metrics.
- `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml`
  - Keep the bubble visually compact while enlarging the actual hit area.
- `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml.cs`
  - Add touch drag/tap handlers and unify pointer drag lifecycle.
- `src/ClassroomToolkit.App/MainWindow.xaml`
  - Remove long-press-only feature settings from hero tiles and add one shared low-frequency overflow.
- `src/ClassroomToolkit.App/MainWindow.Launcher.cs`
  - Route the shared overflow to launcher auto-exit, paint settings, and roll-call settings.
- `src/ClassroomToolkit.App/ViewModels/MainViewModel.cs`
  - Remove feature-setting commands that are no longer bound to hero-tile long press.
- `src/ClassroomToolkit.App/Photos/ImageManagerActivationPolicy.cs`
  - Enable single-tap open for folders and previewable files.
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
  - Add an explicit multi-select entry and keep destructive actions hidden until selection mode is active.
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.State.cs`
  - Trim or demote long-press-only state once explicit selection mode exists.
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs`
  - Make single tap the primary open path and wire the visible multi-select entry.
- `src/ClassroomToolkit.App/Paint/PhotoTouchInteractionPolicy.cs`
  - Shift to finger-first photo browsing: single finger pans regardless of current ink tool, unless whiteboard or active ink operation blocks it.
- `src/ClassroomToolkit.App/Paint/PhotoInputAlignmentPolicy.cs`
  - Let manipulation stay a two-touch zoom owner while single-touch pan is consumed consistently.
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs`
  - Continue using touch pan entry, now with the relaxed single-touch browsing policy.
- `src/ClassroomToolkit.App/TimerSetDialog.xaml`
  - Keep timer controls compact but use touch-friendly stepper semantics.
- `src/ClassroomToolkit.App/TimerSetDialog.xaml.cs`
  - Add touch repeat to the minute steppers so press-and-hold works with fingers.
- `src/ClassroomToolkit.App/AutoExitDialog.xaml.cs`
  - Stop forcing keyboard focus on open.
- `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
  - Add an explicit close affordance and stop treating image tap as the only close path.
- `tests/ClassroomToolkit.Tests/ImageManagerActivationPolicyTests.cs`
  - Update expectations for single-tap open.
- `tests/ClassroomToolkit.Tests/PhotoTouchInteractionPolicyTests.cs`
  - Update finger-pan expectations.
- `tests/ClassroomToolkit.Tests/PhotoInputAlignmentPolicyTests.cs`
  - Update manipulation routing expectations now that finger pan is not cursor-mode-only.
- `tests/ClassroomToolkit.Tests/PhotoTouchInputContractTests.cs`
  - Keep touch-handler coverage aligned with the relaxed touch-pan policy.
- `tests/ClassroomToolkit.Tests/App/OverlayWindowsXamlContractTests.cs`
  - Guard shared styles after photo overlay close affordance is added.
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - Update helper copy now that settings are no longer described as long-press-only.
- `tests/ClassroomToolkit.Tests/App/TimerSetDialogXamlContractTests.cs`
  - Keep timer dialog style coverage after stepper changes.
- `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
  - Update board-button contract coverage for explicit board actions.

## Task 1: Establish Shared Touch Metrics And Hold Accelerators

**Files:**
- Modify: `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`
- Modify: `src/ClassroomToolkit.App/Behaviors/LongPressBehavior.cs`
- Create: `tests/ClassroomToolkit.Tests/App/TouchFirstMetricsXamlContractTests.cs`
- Create: `tests/ClassroomToolkit.Tests/LongPressBehaviorContractTests.cs`

- [ ] **Step 1: Write the failing touch-baseline contract tests**

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class TouchFirstMetricsXamlContractTests
{
    [Fact]
    public void WidgetStyles_ShouldDefineTouchTargetTokens_AndUseThemOnCompactControls()
    {
        var xaml = File.ReadAllText(GetStylesPath());

        xaml.Should().Contain("x:Key=\"Size_Touch_Target_Min\">44</sys:Double>");
        xaml.Should().Contain("x:Key=\"Size_Touch_Target_Primary\">48</sys:Double>");
        xaml.Should().Contain("x:Key=\"Size_Touch_Target_Bubble_Hit\">56</sys:Double>");
        xaml.Should().Contain("x:Key=\"Size_Touch_ScrollBar\">16</sys:Double>");
        xaml.Should().Contain("<Setter Property=\"MinWidth\" Value=\"{StaticResource Size_Touch_Target_Min}\"");
        xaml.Should().Contain("<Setter Property=\"MinHeight\" Value=\"{StaticResource Size_Touch_Target_Min}\"");
        xaml.Should().Contain("<Setter Property=\"Width\" Value=\"{StaticResource Size_Touch_ScrollBar}\"");
    }

    private static string GetStylesPath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Assets",
            "Styles",
            "WidgetStyles.xaml");
    }
}
```

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LongPressBehaviorContractTests
{
    [Fact]
    public void LongPressBehavior_ShouldRegisterTouchEvents_AlongsideMouseEvents()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("element.PreviewTouchDown += OnTouchDown;");
        source.Should().Contain("element.PreviewTouchUp += OnTouchUp;");
        source.Should().Contain("element.LostTouchCapture += OnTouchLostCapture;");
        source.Should().Contain("private static void StartPressTimer(UIElement element)");
        source.Should().Contain("private static bool CompletePress(UIElement? element)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Behaviors",
            "LongPressBehavior.cs");
    }
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TouchFirstMetricsXamlContractTests|FullyQualifiedName~LongPressBehaviorContractTests"
```

Expected:

- FAIL because the touch-size tokens do not exist yet
- FAIL because `LongPressBehavior` only wires mouse events

- [ ] **Step 3: Add touch-size tokens and touch-aware hold wiring**

```xml
<sys:Double x:Key="Size_Touch_Target_Min">44</sys:Double>
<sys:Double x:Key="Size_Touch_Target_Primary">48</sys:Double>
<sys:Double x:Key="Size_Touch_Target_Bubble_Hit">56</sys:Double>
<sys:Double x:Key="Size_Touch_ScrollBar">16</sys:Double>

<Style x:Key="Style_IconButton" TargetType="Button">
    <Setter Property="MinWidth" Value="{StaticResource Size_Touch_Target_Min}"/>
    <Setter Property="MinHeight" Value="{StaticResource Size_Touch_Target_Min}"/>
    <Setter Property="Width" Value="{StaticResource Size_Button_Icon_Regular}"/>
    <Setter Property="Height" Value="{StaticResource Size_Button_Icon_Regular}"/>
</Style>

<Style x:Key="Style_IconButton_Active" TargetType="ToggleButton">
    <Setter Property="MinWidth" Value="{StaticResource Size_Touch_Target_Min}"/>
    <Setter Property="MinHeight" Value="{StaticResource Size_Touch_Target_Min}"/>
    <Setter Property="Width" Value="{StaticResource Size_Button_Icon_Regular}"/>
    <Setter Property="Height" Value="{StaticResource Size_Button_Icon_Regular}"/>
</Style>

<Style x:Key="Style_ColorBubbleToggle" TargetType="ToggleButton">
    <Setter Property="MinWidth" Value="{StaticResource Size_Touch_Target_Min}"/>
    <Setter Property="MinHeight" Value="{StaticResource Size_Touch_Target_Min}"/>
    <Setter Property="Width" Value="{StaticResource Size_Button_Icon_Regular}"/>
    <Setter Property="Height" Value="{StaticResource Size_Button_Icon_Regular}"/>
</Style>

<Style x:Key="Style_ColorPaletteButton" TargetType="Button">
    <Setter Property="MinWidth" Value="{StaticResource Size_Touch_Target_Min}"/>
    <Setter Property="MinHeight" Value="{StaticResource Size_Touch_Target_Min}"/>
</Style>

<Style TargetType="ScrollBar">
    <Setter Property="Width" Value="{StaticResource Size_Touch_ScrollBar}"/>
    <Setter Property="MinWidth" Value="{StaticResource Size_Touch_ScrollBar}"/>
</Style>
```

```csharp
private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    if (d is not UIElement element)
    {
        return;
    }

    element.PreviewMouseLeftButtonDown -= OnMouseDown;
    element.PreviewMouseLeftButtonUp -= OnMouseUp;
    element.MouseLeave -= OnMouseLeave;
    element.PreviewTouchDown -= OnTouchDown;
    element.PreviewTouchUp -= OnTouchUp;
    element.LostTouchCapture -= OnTouchLostCapture;

    if (e.NewValue is not ICommand)
    {
        return;
    }

    element.PreviewMouseLeftButtonDown += OnMouseDown;
    element.PreviewMouseLeftButtonUp += OnMouseUp;
    element.MouseLeave += OnMouseLeave;
    element.PreviewTouchDown += OnTouchDown;
    element.PreviewTouchUp += OnTouchUp;
    element.LostTouchCapture += OnTouchLostCapture;
}

private static void OnTouchDown(object sender, TouchEventArgs e)
{
    if (sender is not UIElement element)
    {
        return;
    }

    StartPressTimer(element);
    element.CaptureTouch(e.TouchDevice);
}

private static void OnTouchUp(object sender, TouchEventArgs e)
{
    if (sender is not UIElement element)
    {
        return;
    }

    var handled = CompletePress(element);
    element.ReleaseTouchCapture(e.TouchDevice);
    e.Handled = handled;
}

private static void OnTouchLostCapture(object sender, TouchEventArgs e)
{
    CancelPress(sender as UIElement, resetTriggered: true);
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TouchFirstMetricsXamlContractTests|FullyQualifiedName~LongPressBehaviorContractTests"
```

Expected:

- PASS for the new contract tests
- no regressions in unrelated compile-time test discovery

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml src/ClassroomToolkit.App/Behaviors/LongPressBehavior.cs tests/ClassroomToolkit.Tests/App/TouchFirstMetricsXamlContractTests.cs tests/ClassroomToolkit.Tests/LongPressBehaviorContractTests.cs
git commit -m "feat: add shared touch target baseline"
```

## Task 2: Add Repeat-Tap Local Settings For Quick Colors And Shapes

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/ToolbarSecondTapTarget.cs`
- Create: `src/ClassroomToolkit.App/Paint/ToolbarSecondTapIntentPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- Modify: `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml.cs`
- Create: `tests/ClassroomToolkit.Tests/ToolbarSecondTapIntentPolicyTests.cs`
- Create: `tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`

- [ ] **Step 1: Write the failing toolbar second-tap tests**

```csharp
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarSecondTapIntentPolicyTests
{
    [Theory]
    [InlineData(false, true, ToolbarSecondTapTarget.QuickColor, ToolbarSecondTapTarget.None)]
    [InlineData(true, false, ToolbarSecondTapTarget.QuickColor, ToolbarSecondTapTarget.None)]
    [InlineData(true, true, ToolbarSecondTapTarget.QuickColor, ToolbarSecondTapTarget.QuickColor)]
    [InlineData(true, true, ToolbarSecondTapTarget.Shape, ToolbarSecondTapTarget.Shape)]
    public void Resolve_ShouldOnlyOpenSecondaryAction_WhenItemIsAlreadySelected_AndSupportsIt(
        bool alreadySelected,
        bool supportsSecondaryAction,
        ToolbarSecondTapTarget requestedTarget,
        ToolbarSecondTapTarget expected)
    {
        ToolbarSecondTapIntentPolicy.Resolve(
            alreadySelected,
            supportsSecondaryAction,
            requestedTarget).Should().Be(expected);
    }
}
```

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintToolbarTouchSettingsContractTests
{
    [Fact]
    public void Toolbar_ShouldWireRepeatTapPreviewHandlers_AndKeepCompactPopoverAccess()
    {
        var xaml = File.ReadAllText(GetToolbarXamlPath());
        var source = File.ReadAllText(GetToolbarSourcePath());

        xaml.Should().Contain("PreviewMouseLeftButtonDown=\"OnQuickColorPointerDown\"");
        xaml.Should().Contain("PreviewTouchDown=\"OnQuickColorTouchDown\"");
        xaml.Should().Contain("PreviewMouseLeftButtonDown=\"OnShapePointerDown\"");
        xaml.Should().Contain("ToolTip=\"颜色 1\"");
        xaml.Should().Contain("ToolTip=\"图形\"");
        source.Should().Contain("ToolbarSecondTapIntentPolicy.Resolve(");
        source.Should().Contain("OpenQuickColorDialog(index.Value);");
        source.Should().Contain("OpenShapeMenu();");
    }

    private static string GetToolbarXamlPath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Paint", "PaintToolbarWindow.xaml");
    private static string GetToolbarSourcePath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Paint", "PaintToolbarWindow.xaml.cs");
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarSecondTapIntentPolicyTests|FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected:

- FAIL because the new policy and popover hooks do not exist yet
- FAIL because helper copy still describes color and shape as long-press-only

- [ ] **Step 3: Add the second-tap policy and compact color/shape popovers**

```csharp
namespace ClassroomToolkit.App.Paint;

internal enum ToolbarSecondTapTarget
{
    None,
    QuickColor,
    Shape,
    Board
}

internal static class ToolbarSecondTapIntentPolicy
{
    internal static ToolbarSecondTapTarget Resolve(
        bool alreadySelected,
        bool supportsSecondaryAction,
        ToolbarSecondTapTarget requestedTarget)
    {
        return alreadySelected && supportsSecondaryAction
            ? requestedTarget
            : ToolbarSecondTapTarget.None;
    }
}
```

```xml
<ToggleButton x:Name="QuickColor1Button"
              PreviewMouseLeftButtonDown="OnQuickColorPointerDown"
              PreviewTouchDown="OnQuickColorTouchDown"
              ToolTip="颜色 1"
              Click="OnColorClick" />

<ToggleButton x:Name="ShapeButton"
              PreviewMouseLeftButtonDown="OnShapePointerDown"
              PreviewTouchDown="OnShapeTouchDown"
              ToolTip="图形"
              Click="OnShapeButtonClick" />
```

```csharp
private ToolbarSecondTapTarget _pendingSecondTapTarget;
private int? _pendingQuickColorIndex;

private void OnQuickColorPointerDown(object sender, MouseButtonEventArgs e)
{
    if (sender is not ToggleButton button)
    {
        return;
    }

    var index = ResolveQuickColorIndex(button.Tag);
    if (!index.HasValue)
    {
        return;
    }

    _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
        alreadySelected: button.IsChecked == true,
        supportsSecondaryAction: true,
        requestedTarget: ToolbarSecondTapTarget.QuickColor);
    _pendingQuickColorIndex = _pendingSecondTapTarget == ToolbarSecondTapTarget.QuickColor
        ? index
        : null;
}

private void OnQuickColorTouchDown(object sender, TouchEventArgs e)
{
    if (sender is not ToggleButton button)
    {
        return;
    }

    var index = ResolveQuickColorIndex(button.Tag);
    if (!index.HasValue)
    {
        return;
    }

    _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
        alreadySelected: button.IsChecked == true,
        supportsSecondaryAction: true,
        requestedTarget: ToolbarSecondTapTarget.QuickColor);
    _pendingQuickColorIndex = _pendingSecondTapTarget == ToolbarSecondTapTarget.QuickColor
        ? index
        : null;
    e.Handled = _pendingSecondTapTarget == ToolbarSecondTapTarget.QuickColor;
}

private void OnShapePointerDown(object sender, MouseButtonEventArgs e)
{
    _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
        alreadySelected: ShapeButton.IsChecked == true,
        supportsSecondaryAction: true,
        requestedTarget: ToolbarSecondTapTarget.Shape);
}

private void OnShapeTouchDown(object sender, TouchEventArgs e)
{
    _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
        alreadySelected: ShapeButton.IsChecked == true,
        supportsSecondaryAction: true,
        requestedTarget: ToolbarSecondTapTarget.Shape);
    e.Handled = _pendingSecondTapTarget == ToolbarSecondTapTarget.Shape;
}

private void OnColorClick(object sender, RoutedEventArgs e)
{
    if (sender is not ToggleButton button)
    {
        return;
    }

    var index = ResolveQuickColorIndex(button.Tag);
    if (!index.HasValue)
    {
        return;
    }

    if (_pendingSecondTapTarget == ToolbarSecondTapTarget.QuickColor && _pendingQuickColorIndex == index.Value)
    {
        button.IsChecked = true;
        OpenQuickColorDialog(index.Value);
        ResetPendingSecondTapState();
        return;
    }

    ResetPendingSecondTapState();
    ApplyQuickColorSelection(index.Value);
}

private void ApplyQuickColorSelection(int index)
{
    PrepareForNonBoardToolbarAction(exitWhiteboard: true);

    var shouldResetShape = _shapeType != PaintShapeType.None;
    var selectedColor = _quickColors[index];

    UpdateQuickColorSelection(selectedColor);
    SelectToolMode(PaintToolMode.Brush, allowToggleOffCurrent: false);

    if (shouldResetShape)
    {
        ResetShapeType();
    }

    if (_overlay != null)
    {
        _overlay.SetBrush(selectedColor, _brushSize, _brushOpacity);
    }

    SafeActionExecutionExecutor.TryExecute(
        () => BrushColorChanged?.Invoke(selectedColor),
        ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: brush color callback failed: {ex.Message}"));
}

private void OnShapeButtonClick(object sender, RoutedEventArgs e)
{
    if (_pendingSecondTapTarget == ToolbarSecondTapTarget.Shape)
    {
        ShapeButton.IsChecked = true;
        OpenShapeMenu();
        ResetPendingSecondTapState();
        return;
    }

    ResetPendingSecondTapState();
    PrepareForNonBoardToolbarAction(exitWhiteboard: true);
    var shapeType = ResolveEffectiveShapeType();
    ApplyShapeType(shapeType);
    SelectToolMode(PaintToolMode.Shape, allowToggleOffCurrent: true);
}

private void ResetPendingSecondTapState()
{
    _pendingSecondTapTarget = ToolbarSecondTapTarget.None;
    _pendingQuickColorIndex = null;
}
```

```csharp
private void BuildButtons()
{
    foreach (var option in Options)
    {
        var button = new System.Windows.Controls.Button
        {
            Width = 36,
            Height = 36,
            Margin = new Thickness(4, 0, 4, 0),
            Background = new SolidColorBrush(option.Color),
            BorderBrush = new SolidColorBrush(GetContrastBorderColor(option.Color)),
            BorderThickness = new Thickness(IsDarkColor(option.Color) ? 2 : 1),
            ToolTip = option.Name,
            Tag = option.Color,
            Style = (Style)FindResource("Style_ColorPaletteButton")
        };
        button.Click += OnColorButtonClick;
        OptionsPanel.Children.Add(button);
    }
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ToolbarSecondTapIntentPolicyTests|FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected:

- PASS for the new policy and contract tests
- PASS for the updated helper-copy contract after long-press-only wording is removed from quick color and shape

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Paint/ToolbarSecondTapTarget.cs src/ClassroomToolkit.App/Paint/ToolbarSecondTapIntentPolicy.cs src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml.cs tests/ClassroomToolkit.Tests/ToolbarSecondTapIntentPolicyTests.cs tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: add compact toolbar second-tap settings"
```

## Task 3: Replace Hidden Board Gestures With Explicit Compact Actions

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/BoardPrimaryAction.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- Create: `tests/ClassroomToolkit.Tests/BoardPrimaryActionTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`

- [ ] **Step 1: Write the failing board-action tests**

```csharp
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BoardPrimaryActionTests
{
    [Theory]
    [InlineData(BoardPrimaryAction.CaptureRegion, BoardPrimaryAction.CaptureRegion)]
    [InlineData(BoardPrimaryAction.EnterWhiteboard, BoardPrimaryAction.EnterWhiteboard)]
    public void LastBoardPrimaryAction_ShouldRoundTrip(BoardPrimaryAction value, BoardPrimaryAction expected)
    {
        value.Should().Be(expected);
    }
}
```

```csharp
[Fact]
public void ToolbarBoardButton_ShouldExposeExplicitBoardActions_InsteadOfDoubleClickOnlyCopy()
{
    var xaml = File.ReadAllText(GetToolbarXamlPath());
    var source = File.ReadAllText(GetToolbarSourcePath());

    xaml.Should().Contain("x:Name=\"BoardActionsPopup\"");
    xaml.Should().Contain("x:Name=\"BoardCaptureActionButton\"");
    xaml.Should().Contain("x:Name=\"BoardWhiteboardActionButton\"");
    xaml.Should().Contain("x:Name=\"BoardColorActionButton\"");
    xaml.Should().Contain("ToolTip=\"截图 / 白板 / 底色\"");
    source.Should().Contain("private BoardPrimaryAction _lastBoardPrimaryAction");
    source.Should().Contain("OnBoardCaptureActionClick");
    source.Should().Contain("OnBoardWhiteboardActionClick");
    source.Should().Contain("OnBoardColorActionClick");
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~BoardPrimaryActionTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected:

- FAIL because the board-action enum and popup do not exist yet
- FAIL because the toolbar contract still assumes hidden double-click / long-press board behavior

- [ ] **Step 3: Add a compact board action popup with last-action memory**

```csharp
namespace ClassroomToolkit.App.Paint;

internal enum BoardPrimaryAction
{
    CaptureRegion,
    EnterWhiteboard
}
```

```xml
<ToggleButton x:Name="BoardButton"
              PreviewMouseLeftButtonDown="OnBoardPointerDown"
              PreviewTouchDown="OnBoardTouchDown"
              ToolTip="截图 / 白板 / 底色"
              Click="OnBoardClick" />

<Popup x:Name="BoardActionsPopup"
       Placement="Top"
       PlacementTarget="{Binding ElementName=BoardButton}"
       StaysOpen="False">
    <Border Style="{StaticResource Style_BubbleShellRoot}" Padding="8">
        <StackPanel>
            <Button x:Name="BoardCaptureActionButton" Content="截图" Click="OnBoardCaptureActionClick"/>
            <Button x:Name="BoardWhiteboardActionButton" Content="白板" Margin="0,6,0,0" Click="OnBoardWhiteboardActionClick"/>
            <Button x:Name="BoardColorActionButton" Content="底色" Margin="0,6,0,0" Click="OnBoardColorActionClick"/>
        </StackPanel>
    </Border>
</Popup>
```

```csharp
private BoardPrimaryAction _lastBoardPrimaryAction = BoardPrimaryAction.CaptureRegion;

private void OnBoardPointerDown(object sender, MouseButtonEventArgs e)
{
    _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
        alreadySelected: BoardButton.IsChecked == true || _boardActive || _regionCapturePending,
        supportsSecondaryAction: true,
        requestedTarget: ToolbarSecondTapTarget.Board);
}

private void OnBoardTouchDown(object sender, TouchEventArgs e)
{
    _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
        alreadySelected: BoardButton.IsChecked == true || _boardActive || _regionCapturePending,
        supportsSecondaryAction: true,
        requestedTarget: ToolbarSecondTapTarget.Board);
    e.Handled = _pendingSecondTapTarget == ToolbarSecondTapTarget.Board;
}

private void OnBoardClick(object sender, RoutedEventArgs e)
{
    if (_pendingSecondTapTarget == ToolbarSecondTapTarget.Board)
    {
        BoardButton.IsChecked = _boardActive || _regionCapturePending;
        BoardActionsPopup.IsOpen = true;
        ResetPendingSecondTapState();
        return;
    }

    ResetPendingSecondTapState();
    ExecuteBoardPrimaryAction(_lastBoardPrimaryAction);
}

private void ExecuteBoardPrimaryAction(BoardPrimaryAction action)
{
    switch (action)
    {
        case BoardPrimaryAction.EnterWhiteboard:
            EnterWhiteboardAction();
            break;
        default:
            BeginRegionCaptureAction();
            break;
    }
}

private void BeginRegionCaptureAction()
{
    ResetToolSelectionBaselineForBoardInteraction();
    ClearNonBoardSelectionVisualState();
    _regionCapturePending = true;
    ShowBoardHint("请框选截图区域");
    RefreshBoardButtonVisualState();
    SafeActionExecutionExecutor.TryExecute(
        () => RegionCaptureRequested?.Invoke(),
        ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: region capture callback failed: {ex.Message}"));
}

private void EnterWhiteboardAction()
{
    ResetToolSelectionBaselineForBoardInteraction();
    ClearNonBoardSelectionVisualState();
    _regionCapturePending = false;
    SetBoardActive(true);
    ShowBoardHint("已进入白板");
}

private void OnBoardCaptureActionClick(object sender, RoutedEventArgs e)
{
    _lastBoardPrimaryAction = BoardPrimaryAction.CaptureRegion;
    BoardActionsPopup.IsOpen = false;
    BeginRegionCaptureAction();
}

private void OnBoardWhiteboardActionClick(object sender, RoutedEventArgs e)
{
    _lastBoardPrimaryAction = BoardPrimaryAction.EnterWhiteboard;
    BoardActionsPopup.IsOpen = false;
    EnterWhiteboardAction();
}

private void OnBoardColorActionClick(object sender, RoutedEventArgs e)
{
    BoardActionsPopup.IsOpen = false;
    OpenBoardColorDialog();
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~BoardPrimaryActionTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected:

- PASS for explicit board-action coverage
- PASS after board helper copy and contract tests are aligned with the new compact popup

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Paint/BoardPrimaryAction.cs src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs tests/ClassroomToolkit.Tests/BoardPrimaryActionTests.cs tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: make board actions explicit"
```

## Checkpoint A: Shared Touch Foundation + Toolbar

- [ ] Run the focused toolbar and touch regression suite

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TouchFirstMetricsXamlContractTests|FullyQualifiedName~LongPressBehaviorContractTests|FullyQualifiedName~ToolbarSecondTapIntentPolicyTests|FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~BoardPrimaryActionTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"
```

Expected:

- PASS for all new shared-touch and toolbar-focused tests

## Task 4: Add A Shared Launcher Overflow And Touch-Friendly Bubble Interaction

**Files:**
- Modify: `src/ClassroomToolkit.App/MainWindow.xaml`
- Modify: `src/ClassroomToolkit.App/MainWindow.Launcher.cs`
- Modify: `src/ClassroomToolkit.App/ViewModels/MainViewModel.cs`
- Modify: `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml`
- Modify: `src/ClassroomToolkit.App/LauncherBubbleWindow.xaml.cs`
- Create: `tests/ClassroomToolkit.Tests/App/LauncherOverflowContractTests.cs`
- Create: `tests/ClassroomToolkit.Tests/LauncherBubbleTouchContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/LauncherBubbleDispatchFallbackContractTests.cs`

- [ ] **Step 1: Write the failing launcher tests**

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherOverflowContractTests
{
    [Fact]
    public void MainWindow_ShouldUseSharedOverflow_ForLowFrequencySettings()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());
        var source = File.ReadAllText(GetLauncherSourcePath());

        xaml.Should().Contain("x:Name=\"LauncherMoreMenu\"");
        xaml.Should().Contain("Header=\"自动关闭\"");
        xaml.Should().Contain("Header=\"画笔设置\"");
        xaml.Should().Contain("Header=\"点名设置\"");
        xaml.Should().NotContain("ToolTip=\"画笔（长按设置）\"");
        xaml.Should().NotContain("ToolTip=\"点名与计时（长按设置）\"");
        source.Should().Contain("OpenLauncherMoreMenu()");
        source.Should().Contain("OnLauncherPaintSettingsMenuClick");
        source.Should().Contain("OnLauncherRollCallSettingsMenuClick");
    }

    private static string GetMainWindowXamlPath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "MainWindow.xaml");
    private static string GetLauncherSourcePath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "MainWindow.Launcher.cs");
}
```

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherBubbleTouchContractTests
{
    [Fact]
    public void LauncherBubble_ShouldKeepSmallVisualBody_ButSupportTouchInput()
    {
        var xaml = File.ReadAllText(GetXamlPath());
        var source = File.ReadAllText(GetSourcePath());

        xaml.Should().Contain("Width=\"64\" Height=\"64\"");
        xaml.Should().Contain("Width=\"42\" Height=\"42\"");
        source.Should().Contain("TouchDown += OnTouchDown;");
        source.Should().Contain("TouchMove += OnTouchMove;");
        source.Should().Contain("TouchUp += OnTouchUp;");
        source.Should().Contain("LostTouchCapture += OnLostTouchCapture;");
    }

    private static string GetXamlPath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "LauncherBubbleWindow.xaml");
    private static string GetSourcePath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "LauncherBubbleWindow.xaml.cs");
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~LauncherOverflowContractTests|FullyQualifiedName~LauncherBubbleTouchContractTests|FullyQualifiedName~UiCopyContractTests|FullyQualifiedName~LauncherBubbleDispatchFallbackContractTests"
```

Expected:

- FAIL because the launcher overflow menu and touch bubble handlers do not exist yet
- FAIL because hero tiles still describe settings as long-press-only

- [ ] **Step 3: Replace hero-tile long-press settings with one shared overflow and add touch bubble handlers**

```xml
<Button Grid.Column="0"
        x:Name="PaintButton"
        Style="{StaticResource Style_WorkShellHeroTileButton}"
        Content="画笔"
        ToolTip="画笔"
        Click="OnPaintClick" />

<Button Grid.Column="1"
        x:Name="RollCallButton"
        Style="{StaticResource Style_WorkShellHeroTileButton}"
        Content="点名与计时"
        ToolTip="点名与计时"
        Click="OnRollCallClick" />

<Button x:Name="SettingsButton"
        Style="{StaticResource Style_WorkShellMiniButton}"
        Tag="{StaticResource Icon_Settings}"
        Click="OnLauncherMoreClick"
        ToolTip="更多"/>

<ContextMenu x:Name="LauncherMoreMenu" Placement="Top">
    <MenuItem Header="自动关闭" Click="OnLauncherAutoExitMenuClick"/>
    <MenuItem Header="画笔设置" Click="OnLauncherPaintSettingsMenuClick"/>
    <MenuItem Header="点名设置" Click="OnLauncherRollCallSettingsMenuClick"/>
</ContextMenu>
```

```csharp
private void OnLauncherMoreClick(object sender, RoutedEventArgs e)
{
    OpenLauncherMoreMenu();
}

private void OpenLauncherMoreMenu()
{
    LauncherMoreMenu.PlacementTarget = SettingsButton;
    LauncherMoreMenu.IsOpen = true;
}

private void OnLauncherAutoExitMenuClick(object sender, RoutedEventArgs e)
{
    OpenAutoExitDialog();
}

private void OnLauncherPaintSettingsMenuClick(object sender, RoutedEventArgs e)
{
    OnOpenPaintSettings();
}

private void OnLauncherRollCallSettingsMenuClick(object sender, RoutedEventArgs e)
{
    OnOpenRollCallSettings();
}

private void OpenAutoExitDialog()
{
    var currentMinutes = Math.Max(0, _settings.LauncherAutoExitSeconds / MainWindowRuntimeDefaults.LauncherMinutesToSeconds);
    var dialog = new AutoExitDialog(currentMinutes)
    {
        Owner = this
    };

    TryFixWindowBorders(this, "launcher-settings", "main-window");
    TryFixWindowBorders(dialog, "launcher-settings", "auto-exit-dialog");

    if (!TryShowDialogWithDiagnostics(dialog, nameof(AutoExitDialog)))
    {
        return;
    }

    _settings.LauncherAutoExitSeconds = Math.Max(0, dialog.Minutes) * MainWindowRuntimeDefaults.LauncherMinutesToSeconds;
    ScheduleAutoExitTimer();
    SaveLauncherSettings();
}
```

```csharp
private int? _activeTouchId;

public LauncherBubbleWindow()
{
    InitializeComponent();
    Cursor = System.Windows.Input.Cursors.Hand;
    MouseLeftButtonDown += OnMouseDown;
    MouseMove += OnMouseMove;
    MouseLeftButtonUp += OnMouseUp;
    TouchDown += OnTouchDown;
    TouchMove += OnTouchMove;
    TouchUp += OnTouchUp;
    LostTouchCapture += OnLostTouchCapture;
    Loaded += OnWindowLoaded;
    IsVisibleChanged += OnWindowVisibleChanged;
    Closed += OnWindowClosed;
}

private void BeginDrag(System.Windows.Point position)
{
    _dragging = true;
    _moved = false;
    _dragOffset = position;
    _dragStartPosition = new System.Windows.Point(Left, Top);
    TryUpdateDragScreenArea(PointToScreen(position));
    BeginDragScope();
}

private void UpdateDrag(System.Windows.Point position)
{
    if (!_dragging)
    {
        return;
    }

    var screen = PointToScreen(position);
    var newX = screen.X - _dragOffset.X;
    var newY = screen.Y - _dragOffset.Y;

    if (!_hasDragScreenArea || !_dragScreenBounds.Contains((int)screen.X, (int)screen.Y))
    {
        TryUpdateDragScreenArea(screen);
    }
    if (!_hasDragScreenArea)
    {
        return;
    }

    newX = Math.Max(_dragWorkingArea.Left, Math.Min(newX, _dragWorkingArea.Right - Width));
    newY = Math.Max(_dragWorkingArea.Top, Math.Min(newY, _dragWorkingArea.Bottom - Height));

    var deltaX = newX - _dragStartPosition.X;
    var deltaY = newY - _dragStartPosition.Y;
    _moved = deltaX * deltaX + deltaY * deltaY > DragThresholdSquared;
    Left = newX;
    Top = newY;
}

private void EndDrag(bool shouldRestoreWhenTap)
{
    _dragging = false;
    _hasDragScreenArea = false;
    EndDragScope();

    if (!_moved && shouldRestoreWhenTap)
    {
        TryExecuteNonFatal(() => RestoreRequested?.Invoke());
        return;
    }

    var center = new System.Windows.Point(Left + Width / 2, Top + Height / 2);
    PlaceNear(center);
    _moved = false;
}

private void OnTouchDown(object sender, TouchEventArgs e)
{
    _activeTouchId = e.TouchDevice.Id;
    BeginDrag(e.GetTouchPoint(this).Position);
    CaptureTouch(e.TouchDevice);
    e.Handled = true;
}

private void OnTouchMove(object sender, TouchEventArgs e)
{
    if (_activeTouchId != e.TouchDevice.Id)
    {
        return;
    }

    UpdateDrag(e.GetTouchPoint(this).Position);
    e.Handled = true;
}

private void OnTouchUp(object sender, TouchEventArgs e)
{
    if (_activeTouchId != e.TouchDevice.Id)
    {
        return;
    }

    EndDrag(shouldRestoreWhenTap: true);
    ReleaseTouchCapture(e.TouchDevice);
    _activeTouchId = null;
    e.Handled = true;
}

private void OnLostTouchCapture(object sender, TouchEventArgs e)
{
    if (_activeTouchId != e.TouchDevice.Id)
    {
        return;
    }

    _dragging = false;
    _hasDragScreenArea = false;
    EndDragScope();
    _activeTouchId = null;
    e.Handled = true;
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~LauncherOverflowContractTests|FullyQualifiedName~LauncherBubbleTouchContractTests|FullyQualifiedName~UiCopyContractTests|FullyQualifiedName~LauncherBubbleDispatchFallbackContractTests"
```

Expected:

- PASS for launcher overflow and bubble touch coverage
- PASS after hero-tile copy no longer references long-press-only settings

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/MainWindow.xaml src/ClassroomToolkit.App/MainWindow.Launcher.cs src/ClassroomToolkit.App/ViewModels/MainViewModel.cs src/ClassroomToolkit.App/LauncherBubbleWindow.xaml src/ClassroomToolkit.App/LauncherBubbleWindow.xaml.cs tests/ClassroomToolkit.Tests/App/LauncherOverflowContractTests.cs tests/ClassroomToolkit.Tests/LauncherBubbleTouchContractTests.cs tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs tests/ClassroomToolkit.Tests/LauncherBubbleDispatchFallbackContractTests.cs
git commit -m "feat: add launcher overflow and touch bubble"
```

## Task 5: Make Image Browsing Single-Tap First And Multi-Select Explicit

**Files:**
- Modify: `src/ClassroomToolkit.App/Photos/ImageManagerActivationPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
- Modify: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.State.cs`
- Modify: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs`
- Modify: `tests/ClassroomToolkit.Tests/ImageManagerActivationPolicyTests.cs`
- Create: `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`

- [ ] **Step 1: Write the failing image-manager tests**

```csharp
using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerActivationPolicyTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void ShouldOpenOnSingleClick_ShouldOpenFolders_AndPreviewableFiles(
        bool isFolder,
        bool isPdf,
        bool isImage,
        bool expected)
    {
        ImageManagerActivationPolicy.ShouldOpenOnSingleClick(isFolder, isPdf, isImage)
            .Should()
            .Be(expected);
    }
}
```

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerTouchFlowContractTests
{
    [Fact]
    public void ImageManager_ShouldExposeVisibleSelectionMode_AndKeepSingleTapOpen()
    {
        var xaml = File.ReadAllText(GetXamlPath());
        var source = File.ReadAllText(GetSourcePath());

        xaml.Should().Contain("x:Name=\"EnterSelectionModeButton\"");
        xaml.Should().Contain("Click=\"OnEnterSelectionModeClick\"");
        source.Should().Contain("private void OnEnterSelectionModeClick");
        source.Should().Contain("EnterMultiSelectMode(");
        source.Should().Contain("ImageManagerActivationPolicy.ShouldOpenOnSingleClick");
        source.Should().Contain("if (item.IsFolder)");
        source.Should().Contain("OpenFolder(item.Path);");
    }

    private static string GetXamlPath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Photos", "ImageManagerWindow.xaml");
    private static string GetSourcePath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Photos", "ImageManagerWindow.Navigation.cs");
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerActivationPolicyTests|FullyQualifiedName~ImageManagerTouchFlowContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected:

- FAIL because single-click open is still disabled for folders and previewable files
- FAIL because the explicit selection-mode entry does not exist yet

- [ ] **Step 3: Enable single-tap open and add an explicit selection-mode entry**

```csharp
namespace ClassroomToolkit.App.Photos;

internal static class ImageManagerActivationPolicy
{
    internal static bool ShouldOpenOnSingleClick(bool isFolder, bool isPdf, bool isImage)
    {
        return isFolder || isPdf || isImage;
    }

    internal static bool ShouldOpenOnDoubleClick(bool isFolder, bool isPdf, bool isImage)
    {
        return isFolder || isPdf || isImage;
    }
}
```

```xml
<Button x:Name="EnterSelectionModeButton"
        Content="多选"
        Margin="12,0,0,0"
        Height="{StaticResource Size_Button_Action_Height_Compact}"
        MinWidth="72"
        Style="{StaticResource Style_SecondaryButton}"
        Click="OnEnterSelectionModeClick"/>

<Button x:Name="DeleteFilesButton"
        Content="删除"
        Margin="6,0,0,0"
        Height="{StaticResource Size_Button_Action_Height_Compact}"
        MinWidth="86"
        Style="{StaticResource Style_DangerButton}"
        Visibility="Collapsed"
        Click="OnDeleteSelectedFilesClick"/>
```

```csharp
private void OnEnterSelectionModeClick(object sender, RoutedEventArgs e)
{
    var sourceList = GetActiveImageList();
    var anchorItem = sourceList.SelectedItem as ImageItem
        ?? ViewModel.Images.FirstOrDefault(item => !item.IsFolder)
        ?? ViewModel.Images.FirstOrDefault();
    if (anchorItem == null)
    {
        return;
    }

    EnterMultiSelectMode(anchorItem, sourceList);
}

private void OnImageListPointerUp(object sender, MouseButtonEventArgs e)
{
    _multiSelectLongPressTimer.Stop();
    if (_longPressTriggered)
    {
        StopLongPressTracking(resetTriggered: true);
        e.Handled = true;
        return;
    }

    if (!TryResolveImageItemFromPointer(sender, e.OriginalSource, out var sourceList, out var item))
    {
        StopLongPressTracking(resetTriggered: true);
        return;
    }

    if (_isMultiSelectMode)
    {
        ToggleMultiSelectItem(sourceList, item);
        e.Handled = true;
        StopLongPressTracking(resetTriggered: true);
        return;
    }

    if (!ImageManagerActivationPolicy.ShouldOpenOnSingleClick(item.IsFolder, item.IsPdf, item.IsImage))
    {
        StopLongPressTracking(resetTriggered: true);
        return;
    }

    if (item.IsFolder)
    {
        OpenFolder(item.Path);
    }
    else
    {
        OpenPreviewItem(item);
    }

    e.Handled = true;
    StopLongPressTracking(resetTriggered: true);
}

private void EnterMultiSelectMode(ImageItem anchorItem, System.Windows.Controls.ListView? sourceList)
{
    _isMultiSelectMode = true;
    EnterSelectionModeButton.Visibility = Visibility.Collapsed;
    DeleteFilesButton.Visibility = Visibility.Visible;
    SelectAllFilesButton.Visibility = Visibility.Visible;
    ExitSelectionModeButton.Visibility = Visibility.Visible;

    ImageList.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;
    ImageListView.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;

    var selectionList = sourceList ?? GetActiveImageList();
    _suppressSelectionChanged = true;
    try
    {
        ImageList.SelectedItems.Clear();
        ImageListView.SelectedItems.Clear();
        selectionList.SelectedItems.Add(anchorItem);
    }
    finally
    {
        _suppressSelectionChanged = false;
    }

    UpdateSelectionActionState();
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerActivationPolicyTests|FullyQualifiedName~ImageManagerTouchFlowContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected:

- PASS for updated single-tap activation expectations
- PASS for explicit selection-mode contract coverage

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Photos/ImageManagerActivationPolicy.cs src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml src/ClassroomToolkit.App/Photos/ImageManagerWindow.State.cs src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs tests/ClassroomToolkit.Tests/ImageManagerActivationPolicyTests.cs tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs
git commit -m "feat: make image manager touch-first"
```

## Task 6: Make Photo/PDF Browsing Finger-First Without Breaking Pinch Zoom

**Files:**
- Modify: `src/ClassroomToolkit.App/Paint/PhotoTouchInteractionPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PhotoInputAlignmentPolicy.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoTouchInteractionPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoInputAlignmentPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/PhotoTouchInputContractTests.cs`

- [ ] **Step 1: Write the failing finger-first browsing tests**

```csharp
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTouchInteractionPolicyTests
{
    [Theory]
    [InlineData(true, false, PaintToolMode.Cursor, false, 1, true)]
    [InlineData(true, false, PaintToolMode.Brush, false, 1, true)]
    [InlineData(true, false, PaintToolMode.Shape, false, 1, true)]
    [InlineData(true, false, PaintToolMode.Cursor, true, 1, false)]
    [InlineData(true, true, PaintToolMode.Brush, false, 1, false)]
    [InlineData(false, false, PaintToolMode.Brush, false, 1, false)]
    public void ShouldUseSingleTouchPan_ShouldFollowFingerFirstRules(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        int activeTouchCount,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            activeTouchCount).Should().Be(expected);
    }
}
```

```csharp
[Theory]
[InlineData(true, false, PaintToolMode.Brush, false, false, 2, 2)]
[InlineData(true, false, PaintToolMode.Brush, false, false, 1, 1)]
[InlineData(true, false, PaintToolMode.Cursor, false, false, 2, 2)]
public void PhotoManipulationRoutingPolicy_ShouldKeepTwoTouchZoom_AndConsumeSingleTouch(
    bool photoModeActive,
    bool boardActive,
    PaintToolMode mode,
    bool inkOperationActive,
    bool photoPanning,
    int activeTouchCount,
    int expected)
{
    var decision = PhotoManipulationRoutingPolicy.Resolve(
        photoModeActive,
        boardActive,
        mode,
        inkOperationActive,
        photoPanning,
        activeTouchCount);

    ((int)decision).Should().Be(expected);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInteractionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~PhotoTouchInputContractTests"
```

Expected:

- FAIL because single-touch pan is still restricted to cursor mode
- FAIL because manipulation routing still consumes brush-mode multi-touch too early

- [ ] **Step 3: Relax single-touch pan ownership and keep manipulation for zoom only**

```csharp
internal static class PhotoTouchInteractionPolicy
{
    internal static bool ShouldUseSingleTouchPan(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        int activeTouchCount)
    {
        return photoModeActive
            && !boardActive
            && !inkOperationActive
            && activeTouchCount == 1;
    }
}
```

```csharp
internal static class PhotoManipulationRoutingPolicy
{
    internal static PhotoManipulationRoutingDecision Resolve(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount)
    {
        if (boardActive)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        if (!photoModeActive)
        {
            return PhotoManipulationRoutingDecision.Ignore;
        }
        if (inkOperationActive || photoPanning)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        return PhotoTouchInteractionPolicy.ShouldUseManipulationZoom(activeTouchCount)
            ? PhotoManipulationRoutingDecision.Handle
            : PhotoManipulationRoutingDecision.Consume;
    }
}
```

```csharp
private void OnTouchDown(object? sender, TouchEventArgs e)
{
    _photoActiveTouchIds.Add(e.TouchDevice.Id);
    StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);

    if (_photoTouchPanDeviceId.HasValue && _photoTouchPanDeviceId != e.TouchDevice.Id)
    {
        EndPhotoPan(allowInertia: false);
    }

    if (!PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(
            _photoModeActive,
            IsBoardActive(),
            _mode,
            IsInkOperationActive(),
            _photoActiveTouchIds.Count))
    {
        return;
    }

    _photoTouchPanDeviceId = e.TouchDevice.Id;
    OverlayRoot.CaptureTouch(e.TouchDevice);
    BeginPhotoPan(
        e.GetTouchPoint(OverlayRoot).Position,
        PhotoPanPointerKind.Touch,
        captureStylus: false);
    MarkPhotoGestureInput();
    e.Handled = true;
}
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoTouchInteractionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~PhotoTouchInputContractTests"
```

Expected:

- PASS for finger-first pan ownership
- PASS for two-touch zoom routing and touch handler contracts

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/Paint/PhotoTouchInteractionPolicy.cs src/ClassroomToolkit.App/Paint/PhotoInputAlignmentPolicy.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Touch.cs tests/ClassroomToolkit.Tests/PhotoTouchInteractionPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoInputAlignmentPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoTouchInputContractTests.cs
git commit -m "feat: make photo browsing finger-first"
```

## Checkpoint B: High-Frequency Touch Surfaces

- [ ] Run the high-frequency surface suite

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~LauncherOverflowContractTests|FullyQualifiedName~LauncherBubbleTouchContractTests|FullyQualifiedName~ImageManagerActivationPolicyTests|FullyQualifiedName~ImageManagerTouchFlowContractTests|FullyQualifiedName~PhotoTouchInteractionPolicyTests|FullyQualifiedName~PhotoInputAlignmentPolicyTests|FullyQualifiedName~PhotoTouchInputContractTests"
```

Expected:

- PASS for launcher, image manager, and photo/PDF touch coverage

## Task 7: Reduce Input Cost In Small Dialogs And Accidental-Close Overlays

**Files:**
- Modify: `src/ClassroomToolkit.App/TimerSetDialog.xaml`
- Modify: `src/ClassroomToolkit.App/TimerSetDialog.xaml.cs`
- Modify: `src/ClassroomToolkit.App/AutoExitDialog.xaml.cs`
- Modify: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
- Modify: `tests/ClassroomToolkit.Tests/App/TimerSetDialogXamlContractTests.cs`
- Create: `tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/OverlayWindowsXamlContractTests.cs`

- [ ] **Step 1: Write the failing dialog/overlay touch tests**

```csharp
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class DialogTouchFlowContractTests
{
    [Fact]
    public void TimerAndAutoExitDialogs_ShouldAvoidMouseOnlyRepeat_AndForcedKeyboardFocus()
    {
        var timerXaml = File.ReadAllText(GetTimerXamlPath());
        var timerSource = File.ReadAllText(GetTimerSourcePath());
        var autoExitSource = File.ReadAllText(GetAutoExitSourcePath());

        timerXaml.Should().Contain("PreviewTouchDown=\"OnMinutesDownTouchDown\"");
        timerXaml.Should().Contain("PreviewTouchDown=\"OnMinutesUpTouchDown\"");
        timerSource.Should().Contain("private void OnMinutesTouchUpOrLostCapture");
        autoExitSource.Should().NotContain("TryKeyboardFocus(MinutesBox, shouldFocus: true)");
    }

    [Fact]
    public void PhotoOverlay_ShouldExposeExplicitCloseButton_AndNotCloseOnImageTap()
    {
        var xaml = File.ReadAllText(GetPhotoOverlayXamlPath());

        xaml.Should().Contain("x:Name=\"CloseButton\"");
        xaml.Should().Contain("Style_OverlayShellCloseButton");
        xaml.Should().NotContain("MouseLeftButtonDown=\"OnCloseClick\"");
    }

    private static string GetTimerXamlPath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "TimerSetDialog.xaml");
    private static string GetTimerSourcePath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "TimerSetDialog.xaml.cs");
    private static string GetAutoExitSourcePath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "AutoExitDialog.xaml.cs");
    private static string GetPhotoOverlayXamlPath() => TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Photos", "PhotoOverlayWindow.xaml");
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TimerSetDialogXamlContractTests|FullyQualifiedName~DialogTouchFlowContractTests|FullyQualifiedName~OverlayWindowsXamlContractTests"
```

Expected:

- FAIL because timer repeat is mouse-only
- FAIL because auto-exit still forces text focus on open
- FAIL because photo overlay still closes on image tap and has no explicit close button

- [ ] **Step 3: Add touch repeat to timer steppers, remove forced focus, and add explicit photo close affordance**

```xml
<Button x:Name="MinutesDownButton"
        Click="OnMinutesDownClick"
        PreviewMouseLeftButtonDown="OnMinutesDownMouseDown"
        PreviewMouseLeftButtonUp="OnMinutesMouseUp"
        PreviewTouchDown="OnMinutesDownTouchDown"
        PreviewTouchUp="OnMinutesTouchUpOrLostCapture"
        LostTouchCapture="OnMinutesTouchUpOrLostCapture" />

<Button x:Name="MinutesUpButton"
        Click="OnMinutesUpClick"
        PreviewMouseLeftButtonDown="OnMinutesUpMouseDown"
        PreviewMouseLeftButtonUp="OnMinutesMouseUp"
        PreviewTouchDown="OnMinutesUpTouchDown"
        PreviewTouchUp="OnMinutesTouchUpOrLostCapture"
        LostTouchCapture="OnMinutesTouchUpOrLostCapture" />
```

```csharp
private void OnMinutesDownTouchDown(object sender, TouchEventArgs e)
{
    if (_updating)
    {
        return;
    }

    DecrementMinutes();
    StartRepeatTimer(isIncrement: false);
    e.Handled = true;
}

private void OnMinutesUpTouchDown(object sender, TouchEventArgs e)
{
    if (_updating)
    {
        return;
    }

    IncrementMinutes();
    StartRepeatTimer(isIncrement: true);
    e.Handled = true;
}

private void OnMinutesTouchUpOrLostCapture(object sender, RoutedEventArgs e)
{
    StopRepeatTimer();
}
```

```csharp
public AutoExitDialog(int minutes)
{
    InitializeComponent();
    MinutesBox.Text = Math.Max(0, minutes).ToString();
    MinutesBox.SelectAll();
    Loaded += OnDialogLoaded;
    Closed += OnDialogClosed;
}
```

```xml
<Rectangle x:Name="BackgroundRect" Fill="Transparent"/>

<Image x:Name="PhotoImage"
       Stretch="Uniform"
       StretchDirection="DownOnly"
       Cursor="Arrow"
       SizeChanged="OnPhotoSizeChanged">
    <Image.Effect>
        <StaticResource ResourceKey="Shadow_Card_Subtle"/>
    </Image.Effect>
</Image>

<Button x:Name="CloseButton"
        Width="{StaticResource Size_Button_Icon_Large}"
        Height="{StaticResource Size_Button_Icon_Large}"
        Style="{StaticResource Style_OverlayShellCloseButton}"
        Click="OnCloseClick"
        Canvas.Right="16"
        Canvas.Top="16"
        Panel.ZIndex="120">
    <Path Data="{StaticResource Icon_Close}" Fill="White" Stretch="Uniform" Width="16" Height="16"/>
</Button>
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TimerSetDialogXamlContractTests|FullyQualifiedName~DialogTouchFlowContractTests|FullyQualifiedName~OverlayWindowsXamlContractTests"
```

Expected:

- PASS for dialog touch-repeat and focus contracts
- PASS for explicit close affordance coverage on photo overlay

- [ ] **Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/TimerSetDialog.xaml src/ClassroomToolkit.App/TimerSetDialog.xaml.cs src/ClassroomToolkit.App/AutoExitDialog.xaml.cs src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml tests/ClassroomToolkit.Tests/App/TimerSetDialogXamlContractTests.cs tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs tests/ClassroomToolkit.Tests/App/OverlayWindowsXamlContractTests.cs
git commit -m "feat: polish dialog and overlay touch flows"
```

## Checkpoint C: Full Verification Before Merge

- [ ] Run the full build gate

```powershell
dotnet build ClassroomToolkit.sln -c Debug
```

Expected:

- `Build succeeded.`
- `0 Warning(s)` or only pre-existing accepted warnings with explicit review

- [ ] Run the full test suite

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
```

Expected:

- PASS for the full suite

- [ ] Run the contract/invariant gate

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
```

Expected:

- PASS for all contract/invariant tests

- [ ] Run the hotspot gate

```powershell
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

Expected:

- `[hotspot] PASS - all .cs files within line budget (max=1200)`

- [ ] Update the implementation evidence file

```markdown
# 20260420-touch-first-low-occlusion-controls-implementation

- rule_id: `R1 R2 R6 R8`
- risk_level: `medium`
- scope: `Touch-first low-occlusion controls implementation`

## 依据
- `docs/superpowers/specs/2026-04-20-touch-first-low-occlusion-controls-design.md`
- `docs/superpowers/plans/2026-04-20-touch-first-low-occlusion-controls.md`

## 变更落点
- list every modified file from the final implementation

## 命令与证据
- capture `codex --version`
- capture `codex --help`
- capture `codex status` or `platform_na`
- capture build / test / contract / hotspot outputs

## 回滚动作
1. `git revert <implementation-commit-range>`
2. rerun the full gate chain
```

## Risks And Watchpoints

| Risk | Impact | Mitigation |
|------|--------|------------|
| Repeat-tap detection fires during drag intent on the toolbar | Medium | Use preview pointer-down only for buttons, keep drag threshold logic on the handle, and reset pending second-tap state on move/leave |
| Larger hit targets visibly bloat compact UI | Medium | Increase minimum hit area first, then keep visual ornament sizes unchanged where possible (`42` visual bubble inside a `64` hit region) |
| Board-action refactor breaks the region-capture workflow | High | Keep board actions as thin wrappers around the current `BeginRegionCaptureAction()` and `EnterWhiteboardAction()` flows; retain contract coverage in `RegionCaptureWhiteboardIntegrationContractTests` |
| Image manager single-tap open causes accidental folder entry during multi-select | Medium | Make explicit selection mode visible and keep delete/select-all controls hidden until the mode is active |
| Finger-first photo panning interferes with active ink operations | High | Preserve the `inkOperationActive` gate and keep two-touch manipulation ownership unchanged for zoom |
| Dialog touch polish introduces unexpected soft-keyboard behavior | Low | Remove forced focus only from low-frequency dialogs and leave explicit text-tap focus unchanged |

## Plan Self-Review

### Spec coverage

- Shared touch primitives and hit-target rules: covered by Task 1.
- Toolbar repeat-tap local settings: covered by Task 2.
- Explicit board actions with low occlusion: covered by Task 3.
- Shared launcher overflow and refined bubble: covered by Task 4.
- Single-tap image browsing and explicit multi-select: covered by Task 5.
- Finger-first photo/PDF browsing: covered by Task 6.
- Low-frequency dialog and overlay polish: covered by Task 7.
- Final gate sequence and evidence: covered by Checkpoint C.

### Placeholder scan

- No `TODO`, `TBD`, `implement later`, or unresolved file placeholders remain in this plan.

### Type consistency

- `ToolbarSecondTapTarget`, `ToolbarSecondTapIntentPolicy`, and `BoardPrimaryAction` are introduced once and reused consistently across later tasks.
- `OnLauncherPaintSettingsMenuClick`, `OnLauncherRollCallSettingsMenuClick`, and `OpenLauncherMoreMenu()` are named consistently between XAML and code-behind.
- `BeginRegionCaptureAction()` and `EnterWhiteboardAction()` are referenced consistently as the two explicit board primary flows.
