# 20260419-crosspage-zoom-sync

- issue_id: photo-zoom-crosspage-lag-desync
- attempt_count: 1
- clarification_mode: direct_fix
- rule_ids: R1,R2,R3,R6,R8
- risk_level: medium
- scope:
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Bounds.cs

## Basis
- 现象：PDF/图片全屏缩放延迟明显；跨页显示时页面缩放速度不同步。
- 根因：当前页缩放立即生效（主图 Transform），邻页缩放依赖 `RequestCrossPageDisplayUpdate -> UpdateCrossPageDisplay` 重渲染路径，导致高频缩放下邻页滞后。

## Changes
1. 为 `UpdateNeighborTransformsForPan` 增加 `includeScale` 参数（默认 false），在缩放时可同步更新邻页 `Scale + Translate`，不等待重渲染。
2. 在 `ApplyPhotoScale` 中：
   - 跨页模式下先执行 `UpdateNeighborTransformsForPan(includeScale: true)`，保证可见邻页即时跟随。
   - 缩放请求改为 `WithImmediate(ApplyScale)`，降低调度延迟。
   - 手势缩放 (`_photoManipulating`) 期间不重复触发 `ApplyScale` 更新请求，避免与 `ManipulationDelta` 的跨页刷新叠加造成抖动/积压。

## Commands / Evidence
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 1
  - key_output: `MSB3027/MSB3021`，`ClassroomToolkit.App\bin\Debug\net10.0-windows\*.dll` 被 `sciman Classroom Toolkit (21524)` 与 `Microsoft Visual Studio (81664)` 锁定。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: 1
  - key_output: 同上，因 App 输出 DLL 被锁定导致复制失败。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: 1
  - key_output: 同上，因 App 输出 DLL 被锁定导致复制失败。

## N/A Records
- type: gate_na
  - gate: build
  - reason: 运行中进程占用目标输出 DLL，构建拷贝失败（MSB3027/MSB3021）。
  - alternative_verification: 静态代码复核 + `git diff` 检查仅两处目标文件变更，且逻辑闭环与现有调用兼容（新增参数有默认值）。
  - evidence_link: `docs/change-evidence/20260419-crosspage-zoom-sync.md`
  - expires_at: 2026-04-20T23:59:59+08:00
- type: gate_na
  - gate: test
  - reason: 依赖 build 成功；同一 DLL 锁导致测试阶段编译失败。
  - alternative_verification: 逻辑路径审查（缩放事件 -> 邻页变换即时同步 -> 仍保留完整重绘兜底）。
  - evidence_link: `docs/change-evidence/20260419-crosspage-zoom-sync.md`
  - expires_at: 2026-04-20T23:59:59+08:00
- type: gate_na
  - gate: contract/invariant
  - reason: 依赖 build 成功；同一 DLL 锁导致契约测试编译失败。
  - alternative_verification: 变更未改动架构依赖与 Interop 合同，仅影响 Photo 跨页变换时序。
  - evidence_link: `docs/change-evidence/20260419-crosspage-zoom-sync.md`
  - expires_at: 2026-04-20T23:59:59+08:00

## Hotspot Review
- hotspot_files:
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Bounds.cs
- checks:
  1. 默认参数保证原有调用（平移路径）行为不变。
  2. 缩放同步路径只改可见邻页变换，不改位图缓存与解码策略。
  3. 手势场景避免 `ApplyScale` 与 `ManipulationDelta` 双重请求导致调度积压。
  4. 仍保留跨页完整更新请求（非手势场景 immediate），缩放后可补齐新进入视口页。

## Rollback
- `git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Bounds.cs`

## Round-2 Fix (attempt_count=2)
- 新增“缩放交互窗口”（180ms）：滚轮/手势缩放后短窗口内视为交互中。
- 交互态下禁止邻页同步解析/渲染：
  - `CrossPageNeighborHeightResolvePolicy` 在 `interactionActive=true` 时返回 `false`。
  - `CrossPageNeighborBitmapResolvePolicy` 在 `interactionActive=true` 时返回 `false`。
- 缩放输入打点：`MarkPhotoZoomInput()`，并在跨页交互判定中合并 `IsPhotoZoomInteractionActive()`。

### Round-2 Command Evidence
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 1
  - key_output: `MSB3027/MSB3021`，输出 DLL 仍被 `sciman Classroom Toolkit (94220)` 与 `Microsoft Visual Studio (95808)` 锁定。

### Round-2 Hotspot
- src/ClassroomToolkit.App/Paint/CrossPageNeighborHeightResolvePolicy.cs
- src/ClassroomToolkit.App/Paint/CrossPageNeighborBitmapResolvePolicy.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Helpers.cs
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Telemetry.cs
- 结论：高频缩放期间，跨页邻页只走缓存帧与异步补齐，避免 UI 线程同步邻页解码/渲染。

## Round-3 Fix (attempt_count=3)
- clarification_mode: direct_fix
- clarification_questions: []
- clarification_answers: []

### Basis
- 用户复现澄清：问题不是页面消失，而是各页先在原地缩小，等下一轮位移后才重新拼接，空白因此短暂出现。
- 根因：邻页 `img.Tag / inkImg.Tag` 保存的是已经乘过 `_photoScale` 的 `baseTop`，`_neighborPageHeightCache` 保存的也是当前缩放下的页高。缩放时主图 `Scale + Translate` 立即更新，但这些“已缩放位移/页高”仍保持旧倍率，直到异步 `UpdateCrossPageDisplay()` 重新布局后才校正，形成肉眼可见的空白。

### Changes
1. 新增 `CrossPageZoomLayoutScalePolicy`，统一判断缩放倍率是否需要同步传播，并提供线性缩放规则。
2. 在 `ApplyPhotoScale()` 中计算 `layoutScaleFactor = newScale / currentScale`，跨页模式下先执行 `SyncNeighborLayoutForZoom(layoutScaleFactor)`，再更新邻页 transform。
3. `SyncNeighborLayoutForZoom()` 会同步缩放：
   - 当前已显示邻页槽位的 `baseTop`
   - 对应 ink 槽位的 `BaseTop`
   - `_neighborPageHeightCache` 中的页高
4. 保留现有 `RequestCrossPageDisplayUpdate(CrossPageUpdateSources.ApplyScale)` 作为后续可见页补齐/重排兜底，但不再依赖它来修正当前可见页的拼接位置。

### Commands / Evidence
- `codex --version`
  - exit_code: 0
  - key_output: `codex-cli 0.121.0`
- `codex --help`
  - exit_code: 0
  - key_output: `Codex CLI`
- `codex status`
  - exit_code: 1
  - key_output: `Error: stdin is not a terminal`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 1
  - key_output: `MSB3027/MSB3021`，`sciman Classroom Toolkit (31548)` 占用输出文件。
- `Stop-Process -Id 31548 -Force`
  - exit_code: 0
  - key_output: `process terminated`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 0
  - key_output: `0 Warning(s) / 0 Error(s)`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: 0
  - key_output: `Passed: 3295`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: 0
  - key_output: `Passed: 28`

### N/A Records
- type: platform_na
  - gate: codex-status
  - reason: 非交互终端下 `codex status` 不支持，返回 `stdin is not a terminal`。
  - alternative_verification: 使用 `codex --version` 与 `codex --help` 验证 CLI 可用，并在最终结论中记录 `active_rule_path=AGENTS.md (repo root)`。
  - evidence_link: `docs/change-evidence/20260419-crosspage-zoom-sync.md`
  - expires_at: 2026-04-20T23:59:59+08:00

### Hotspot Review
- hotspot_files:
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Bounds.cs
  - src/ClassroomToolkit.App/Paint/CrossPageZoomLayoutScalePolicy.cs
  - tests/ClassroomToolkit.Tests/CrossPageZoomLayoutScalePolicyTests.cs
- checks:
  1. `baseTop` 与缓存页高本质上都是“已乘当前缩放倍率的 DIP 值”，在统一缩放下按 `newScale / oldScale` 线性传播是精确等价，不是经验修补。
  2. 缩放锚点仍由 `ApplyPhotoScale()` 的 `ToPhotoSpace(center)` 逻辑控制，本次未改当前页锚点数学，只修正邻页跟随时序。
  3. 同步传播只操作现有槽位 tag 与缓存，不引入新的同步解码或同步 PDF 渲染，不会把上一轮卡顿问题重新带回 UI 线程。
  4. 异步 `ApplyScale` 跨页刷新仍保留，负责新进入视口页和可见页集合变更；当前可见页不再等待它来消除缝隙。

### Rollback
- `git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Bounds.cs src/ClassroomToolkit.App/Paint/CrossPageZoomLayoutScalePolicy.cs tests/ClassroomToolkit.Tests/CrossPageZoomLayoutScalePolicyTests.cs`
