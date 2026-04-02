# sciman Classroom Toolkit UI 视觉终态重构 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 ClassroomToolkit 的全部窗口、公共控件样式和交互文案收口为“深色专业课堂台”终态视觉方案，同时保持课堂高频场景下的稳定性与流畅性。

**Architecture:** 先收口 `Colors.xaml + WidgetStyles.xaml + App.xaml` 的 Foundation/Controls/Shell，再按 `WorkShell -> Dialog/ManagementShell -> Fullscreen/工具浮窗` 的顺序回收局部样式岛。所有可回归的视觉要求优先转成现有 `xUnit` 契约测试，窗口 XAML 只保留结构与资源引用，不新增行为编排职责。

**Tech Stack:** .NET 10, WPF, XAML ResourceDictionary, xUnit, FluentAssertions, 现有 XAML contract tests, PowerShell / `dotnet` build & test。

---

## Preconditions And Source Of Truth

- 我正在使用 `writing-plans` skill 来创建实施计划。
- 设计基线：
  - `E:\PythonProject\ClassroomToolkit\docs\superpowers\specs\2026-03-15-ui-visual-endstate-design.md`
- 项目约束：
  - 不调整业务逻辑、数据结构、配置格式、快捷键语义
  - 不把业务/Windowing/Interop 职责挪回 `MainWindow.*`、`RollCallWindow.*`、`PaintOverlayWindow.*`
  - 不新增高成本实时特效
- 执行前要求：
  - 在独立 worktree 中执行实现工作
  - 先检查 `git status --short`
  - 不触碰当前工作区中与本任务无关的 `AGENTS.md / CLAUDE.md / GEMINI.md / GlobalUser/*` 改动
- 验证基线：
  - 视觉改造优先依赖现有 `tests\ClassroomToolkit.Tests\App\*.cs` 契约测试
  - 每个 chunk 至少跑对应的 targeted tests 和一次 `dotnet build`
- 提交规则：
  - 仅在用户明确要求时执行 `git commit`

## File Structure

### Foundation / Shared Styles

- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Assets\Styles\Colors.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Assets\Styles\WidgetStyles.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\App.xaml`

### WorkShell Windows

- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.xaml`

### Dialog / Settings / Management Windows

- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\AboutDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\AutoExitDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RemoteKeyDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\TimerSetDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallSettingsDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Ink\InkSettingsDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\ClassSelectDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\BoardColorDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintSettingsDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\StudentListDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Photos\ImageManagerWindow.xaml`
- Optional touch if visual cleanup needs shell alignment only: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Diagnostics\DiagnosticsDialog.xaml`

### Floating / Fullscreen / Tool Windows

- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintToolbarWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\QuickColorPaletteWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\LauncherBubbleWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Photos\PhotoOverlayWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Photos\RollCallGroupOverlayWindow.xaml`

### Test Files To Extend

- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\ThemeResourceDictionaryTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetStylesContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetTypographyContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetDensityContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetShellSizeContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\IconGlyphTokenUsageXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\MainWindowXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\RollCallWindowXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\ManagementWindowsXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\SettingsDialogsXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\OverlayWindowsXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\TimerSetDialogXamlContractTests.cs`
- Create: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs`

## Chunk 1: Foundation Tokens And Shared Controls

### Task 1: Lock the visual end-state into failing contract tests first

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\ThemeResourceDictionaryTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetStylesContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetTypographyContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetDensityContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetShellSizeContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\IconGlyphTokenUsageXamlContractTests.cs`
- Create: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs`

- [ ] **Step 1: Add failing assertions for the new end-state contract**

Add assertions for:
- new or tightened semantic color/brush keys
- shared shell/card/clickable-panel style keys
- icon size token usage on core windows
- typography/density token ranges
- copy rules on critical windows (`Tooltip`, empty-state text, action labels)

- [ ] **Step 2: Run the targeted contract suite and verify it fails**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ThemeResourceDictionaryTests|FullyQualifiedName~WidgetStylesContractTests|FullyQualifiedName~WidgetTypographyContractTests|FullyQualifiedName~WidgetDensityContractTests|FullyQualifiedName~WidgetShellSizeContractTests|FullyQualifiedName~IconGlyphTokenUsageXamlContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected: FAIL because the new token/style/copy expectations are not implemented yet.

- [ ] **Step 3: Implement the minimal test skeleton and helper logic**

Keep the new copy contract focused on literal XAML strings and avoid parsing full visual trees. Use the same repository-root helper pattern already present in the existing contract tests.

- [ ] **Step 4: Re-run the targeted contract suite**

Run the same command as Step 2.

Expected: Tests compile; resource/copy assertions still fail until Task 2 updates XAML and dictionaries.

- [ ] **Step 5: Checkpoint**

Do not commit unless the user explicitly asks for a commit.

### Task 2: Rebuild Foundation tokens and shared control families in one pass

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Assets\Styles\Colors.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Assets\Styles\WidgetStyles.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\App.xaml`
- Modify if needed by contract fallout only: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\ThemeResourceDictionaryTests.cs`
- Modify if needed by contract fallout only: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\WidgetStylesContractTests.cs`

- [ ] **Step 1: Update semantic tokens in `Colors.xaml`**

Implement the end-state token refresh:
- deepen the Slate base
- keep `Primary / Teaching / Warning / Danger` semantics stable
- keep compatibility aliases required by current XAML
- prefer shared frozen brushes and shared shadow resources

- [ ] **Step 2: Centralize all shared shell/control styles in `WidgetStyles.xaml`**

Add or tighten:
- compact button density
- icon button size families
- shared clickable-card styles
- shared shell titlebar/content/footer sizing
- full-screen side rail / close / hint styles
- dialog popup and palette surface styles

- [ ] **Step 3: Remove style gaps that force windows to keep local style islands**

Specifically provide reusable replacements for:
- hero tile / mini tool patterns
- color bubble / color block patterns
- student card / selectable card patterns
- management thumbnail item patterns
- setting-card hover border patterns

- [ ] **Step 4: Run the Foundation contract suite**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ThemeResourceDictionaryTests|FullyQualifiedName~WidgetStylesContractTests|FullyQualifiedName~WidgetTypographyContractTests|FullyQualifiedName~WidgetDensityContractTests|FullyQualifiedName~WidgetShellSizeContractTests"
```

Expected: PASS with the new token/style contract intact.

- [ ] **Step 5: Build the app project**

Run:
```powershell
dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug
```

Expected: PASS with no XAML resource resolution errors.

## Chunk 2: WorkShell Windows And Core Classroom Flow

### Task 3: Convert `MainWindow` into the end-state launcher shell

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\MainWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\MainWindowXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs`

- [ ] **Step 1: Add failing launcher-shell assertions**

Cover:
- shared hero tile style usage instead of local `Window.Resources`
- shared mini tool button usage
- compact icon-size token usage
- tightened action copy and tooltip copy

- [ ] **Step 2: Run the `MainWindow` contract tests to verify they fail**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowXamlContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected: FAIL on missing shared style references and/or stale copy strings.

- [ ] **Step 3: Replace local style islands with shared resources**

Move `HeroTile`, `MiniTool`, `MiniDanger` usage to the shared style family and keep `MainWindow.xaml` focused on structure, bindings, and command hookup only.

- [ ] **Step 4: Update launcher copy**

Shorten and unify:
- button labels
- tooltips
- bottom-bar hints

Keep labels short and move any extra explanation into tooltip text only where needed.

- [ ] **Step 5: Re-run targeted tests and build**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowXamlContractTests|FullyQualifiedName~UiCopyContractTests"
dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug
```

Expected: PASS.

### Task 4: Bring `RollCallWindow` to the same WorkShell standard

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\RollCallWindowXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs`

- [ ] **Step 1: Add failing assertions for WorkShell alignment**

Cover:
- removal of inline local styles where shared resources should exist
- icon token usage
- timer/roll-call action button consistency
- clear copy for mode toggle, class selection, timer controls, and loading overlay

- [ ] **Step 2: Run the `RollCallWindow` contract tests and verify they fail**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallWindowXamlContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected: FAIL on shared-style and copy assertions.

- [ ] **Step 3: Replace inline/local styles with shared styles**

Extract or swap:
- mode-toggle path style
- group-button selected-state pattern
- timer play/pause icon state pattern

Prefer a reusable shared style or shared visual state pattern over repeated inline XAML.

- [ ] **Step 4: Tighten copy and loading text**

Make button/tooltip/loading text concise and consistent with the copy rules from the spec.

- [ ] **Step 5: Re-run targeted tests and build**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallWindowXamlContractTests|FullyQualifiedName~UiCopyContractTests"
dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug
```

Expected: PASS.

## Chunk 3: Dialog, Settings, And Management Shell Consolidation

### Task 5: Normalize all dialogs and settings windows onto shared shell/card patterns

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\AboutDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\AutoExitDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RemoteKeyDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\TimerSetDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\RollCallSettingsDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Ink\InkSettingsDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\ClassSelectDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\BoardColorDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintSettingsDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\SettingsDialogsXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\TimerSetDialogXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\IconGlyphTokenUsageXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs`

- [ ] **Step 1: Add failing shell-alignment and copy assertions**

Assert that settings/dialog windows:
- use `DialogShell` border/title/action patterns
- avoid legacy inline background keys and inline `DropShadowEffect`
- use icon size tokens consistently
- keep copy concise on titles, buttons, preset labels, and helper text

- [ ] **Step 2: Run the targeted dialog/settings test suite**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~SettingsDialogsXamlContractTests|FullyQualifiedName~TimerSetDialogXamlContractTests|FullyQualifiedName~IconGlyphTokenUsageXamlContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected: FAIL on windows that still use hand-written shells or outdated copy.

- [ ] **Step 3: Swap hand-written shells for shared shell resources**

Focus on:
- `RollCallSettingsDialog`
- `InkSettingsDialog`
- `ClassSelectDialog`
- `BoardColorDialog`
- `PaintSettingsDialog`

Use shared dialog shell, shared setting-card patterns, and shared compact action sizing.

- [ ] **Step 4: Tighten dialog copy**

Shorten:
- title/subtitle wording
- preset/help text where it is redundant
- action labels and tooltip strings

Preserve warnings that encode real risk or scope, but compress wording.

- [ ] **Step 5: Re-run targeted tests and build**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~SettingsDialogsXamlContractTests|FullyQualifiedName~TimerSetDialogXamlContractTests|FullyQualifiedName~IconGlyphTokenUsageXamlContractTests|FullyQualifiedName~UiCopyContractTests"
dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug
```

Expected: PASS.

### Task 6: Convert management windows into one compact management surface family

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\StudentListDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Photos\ImageManagerWindow.xaml`
- Modify if shell cleanup is needed only: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Diagnostics\DiagnosticsDialog.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\ManagementWindowsXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs`

- [ ] **Step 1: Add failing management-surface assertions**

Cover:
- shared management shell usage
- shared clickable-card/list-item styles
- shared density in toolbars, split panes, and footer actions
- concise empty-state and toolbar copy

- [ ] **Step 2: Run the management-window contract suite and verify it fails**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ManagementWindowsXamlContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected: FAIL on local card/list item styles and stale copy.

- [ ] **Step 3: Replace local card/list/item styles with shared resources**

Specifically remove or reduce local style islands in:
- `StudentListDialog`
- `ImageManagerWindow`

Keep only structural data templates that cannot reasonably move into a shared style.

- [ ] **Step 4: Rewrite management copy for clarity**

Tighten:
- section titles
- empty-state prompts
- toolbar tooltips
- footer helper text

- [ ] **Step 5: Re-run targeted tests and build**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ManagementWindowsXamlContractTests|FullyQualifiedName~UiCopyContractTests"
dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug
```

Expected: PASS.

## Chunk 4: Floating Tools, Fullscreen Surfaces, And Final Verification

### Task 7: Unify floating and fullscreen windows around one compact overlay language

**Files:**
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintToolbarWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\QuickColorPaletteWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\LauncherBubbleWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Photos\PhotoOverlayWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Paint\PaintOverlayWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\src\ClassroomToolkit.App\Photos\RollCallGroupOverlayWindow.xaml`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\OverlayWindowsXamlContractTests.cs`
- Modify: `E:\PythonProject\ClassroomToolkit\tests\ClassroomToolkit.Tests\App\UiCopyContractTests.cs`

- [ ] **Step 1: Add failing overlay/floating assertions**

Cover:
- shared fullscreen close/side-rail/hint styles
- shared floating palette / bubble / selectable-color style usage
- no new inline heavy effects
- concise overlay help text and tooltip text

- [ ] **Step 2: Run the overlay contract suite and verify it fails**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayWindowsXamlContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected: FAIL where local bubble/palette/toolbar styles remain.

- [ ] **Step 3: Replace local floating styles with shared overlay resources**

Converge:
- color bubble / palette block
- launcher bubble shell
- photo/fullscreen close and side rails
- group overlay badge shell

Keep the visuals light enough for topmost and fullscreen scenarios.

- [ ] **Step 4: Tighten overlay copy**

Simplify:
- close hints
- export/fit-width tooltips
- toolbar tooltips
- short helper badges

- [ ] **Step 5: Re-run targeted tests and build**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayWindowsXamlContractTests|FullyQualifiedName~UiCopyContractTests"
dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug
```

Expected: PASS.

### Task 8: Run the full visual regression gate and manual smoke matrix

**Files:**
- Modify only if fallout appears: files touched in Tasks 1-7
- Verification only: no new source file expected

- [ ] **Step 1: Run the full App-side contract suite**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ThemeResourceDictionaryTests|FullyQualifiedName~Widget|FullyQualifiedName~MainWindowXamlContractTests|FullyQualifiedName~RollCallWindowXamlContractTests|FullyQualifiedName~ManagementWindowsXamlContractTests|FullyQualifiedName~SettingsDialogsXamlContractTests|FullyQualifiedName~OverlayWindowsXamlContractTests|FullyQualifiedName~TimerSetDialogXamlContractTests|FullyQualifiedName~IconGlyphTokenUsageXamlContractTests|FullyQualifiedName~UiCopyContractTests"
```

Expected: PASS.

- [ ] **Step 2: Run the full test suite**

Run:
```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
```

Expected: PASS with no regressions outside visual-contract coverage.

- [ ] **Step 3: Run the solution build**

Run:
```powershell
dotnet build ClassroomToolkit.sln -c Debug
```

Expected: PASS.

- [ ] **Step 4: Execute manual smoke checks on critical windows**

Verify:
- launcher shell density and tooltip clarity
- roll-call / timer controls and loading text
- paint settings and roll-call settings readability
- image manager split panes, list density, empty-state text
- fullscreen photo/PDF controls and help badges
- floating toolbar and palette compact sizing

Expected: No clipped text, no missing resources, no obviously heavy effects, no perceivable delay beyond current baseline.

- [ ] **Step 5: Checkpoint**

If the user requests integration after review, continue with `superpowers:subagent-driven-development`. Do not commit unless explicitly instructed.

## Natural Chunk Review Handoff

- Review Chunk 1 after Task 2 completes.
- Review Chunk 2 after Task 4 completes.
- Review Chunk 3 after Task 6 completes.
- Review Chunk 4 after Task 8 completes.

For each review:
- compare implementation against `E:\PythonProject\ClassroomToolkit\docs\superpowers\specs\2026-03-15-ui-visual-endstate-design.md`
- verify the touched XAML removed local style islands instead of adding new ones
- verify copy tightened rather than expanded
- verify no performance-hostile effects were introduced

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-03-15-ui-visual-endstate-implementation-plan.md`. Ready to execute?

