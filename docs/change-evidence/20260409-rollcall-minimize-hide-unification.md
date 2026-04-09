# 2026-04-09 点名窗口最小化/隐藏路径归一修复

## 1) 依据
- 用户反馈：点名窗口点击“最小化”后，遥控点名语音/照片明显延迟，且在 PDF/图片全屏、白板中左上角分组名不显示。
- 目标：`RollCallWindow` 的“最小化”与启动器“隐藏点名窗口”走完全一致的功能实现路径，并保持遥控点名与组名覆盖层稳定。

## 2) 变更落点
- `src/ClassroomToolkit.App/MainWindow.xaml.cs`
- `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
- `src/ClassroomToolkit.App/RollCallWindow.Input.cs`
- `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml.cs`

## 3) 关键改动
- 新增 `HideRollCallWindowFromChildRequest()`，复用启动器隐藏点名窗口的同一 transition 流程（含 z-order 请求路径）。
- `RollCallWindow` 最小化按钮改为优先调用 `MainWindow.HideRollCallWindowFromChildRequest()`；仅在无主窗口时回退 `HideRollCall()`。
- 遥控动作 UI 调度从 `DispatcherPriority.Background` 提升为 `DispatcherPriority.Normal`（仅入口调度）。
- 照片覆盖层、分组覆盖层在 `Show()` 后执行 `WindowTopmostExecutor.ApplyNoActivate(..., enforceZOrder: true)`，增强全屏场景可见性。

## 4) 执行命令与证据
- precheck
  - `Get-Command dotnet` -> `dotnet.exe` 存在
  - `Get-Command powershell` -> `powershell.exe` 存在
  - `Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj` -> `True`
- gate
  - `dotnet build ClassroomToolkit.sln -c Debug` -> pass
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> pass (3200 passed)
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> pass (25 passed)
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `[hotspot] status=PASS`

## 5) N/A 记录
- `platform_na`
  - reason: 非交互终端下 `codex status` 返回 `stdin is not a terminal`
  - alternative_verification: 执行 `codex --version` 与 `codex --help` 成功，结合仓库内实际改动与门禁输出作为有效执行证据
  - evidence_link: 本文件第 4 节
  - expires_at: `2026-05-09`

## 6) 风险与回滚
- 风险等级：中（窗口可见性与调度优先级链路调整）
- 回滚动作：
  1. 回退上述 5 个文件改动（`git checkout -- <file>` 或等价反向提交）
  2. 重新执行门禁顺序：`build -> test -> contract/invariant -> hotspot`

## 7) 澄清协议留痕
- `issue_id`: rollcall-minimize-hide-unification-20260409
- `attempt_count`: 1（中途仅出现一次编译错误自修复）
- `clarification_mode`: direct_fix
- `clarification_scenario`: bugfix
- `clarification_questions`: none
- `clarification_answers`: none

---

## 8) 第二轮增量修复（层级漂移）
- 触发原因：用户复测反馈“延迟改善，但 PDF/图片全屏、白板中分组名仍不显示，照片仍偶发不显示”。
- 根因判断：主窗口 `EnsureCriticalFloatingWindowsTopmost(...)` 仅持续管理 `toolbar/rollcall/launcher`，未持续纳管 `RollCallGroupOverlayWindow/PhotoOverlayWindow`，导致后续前台窗口重排时可能被压层。
- 增量改动：
  - `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
    - 新增 `RetouchAuxOverlayWindowsTopmost(bool enforceZOrder)`，统一对 `_photoOverlay/_groupOverlay` 执行 `WindowTopmostExecutor.ApplyNoActivate(...)`。
  - `src/ClassroomToolkit.App/MainWindow.xaml.cs`
    - 在 `EnsureCriticalFloatingWindowsTopmost(...)` 中追加 `_rollCallWindow?.RetouchAuxOverlayWindowsTopmost(enforceZOrder);`，把点名附属覆盖层并入主层级修复循环。
- 第二轮门禁结果（同顺序）：
  - `dotnet build ClassroomToolkit.sln -c Debug` -> pass
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> pass (3200 passed)
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> pass (25 passed)
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `[hotspot] status=PASS`
- 澄清协议留痕更新：
  - `issue_id`: rollcall-minimize-hide-unification-20260409
  - `attempt_count`: 2
  - `clarification_mode`: direct_fix
  - `clarification_scenario`: bugfix
  - `clarification_questions`: none
  - `clarification_answers`: none

---

## 9) 第三轮增量修复（统一前台守护）
- 触发原因：用户反馈“仍偶发分组名/照片/照片顶部姓名不显示，启动器（含悬浮球）偶发不在最前台”。
- 根因判断：单次 `Show()`/事件触发的置顶修补仍可能漏掉后续系统窗口重排；需要持续、统一、低频的 force retouch 通道。
- 增量改动：
  - `src/ClassroomToolkit.App/Windowing/FloatingTopmostWatchdogPolicy.cs`
    - 新增前台守护策略：700ms 周期；当 `toolbar/rollcall/launcher/imageManager/rollcall-aux-overlay` 任一可见时，允许触发统一强制重排。
  - `src/ClassroomToolkit.App/MainWindow.xaml.cs`
    - 新增 `_floatingTopmostWatchdogTimer`，`Loaded` 启动，`Closed` 停止与解注册。
    - 新增 `OnFloatingTopmostWatchdogTick(...)`：命中守护条件时调用 `RequestApplyZOrderPolicy(forceEnforceZOrder: true)`，走既有统一协调器。
  - `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
    - 新增 `HasVisibleAuxOverlay()`，供守护器判断点名附属覆盖层活跃状态。
- 第三轮门禁结果（同顺序）：
  - `dotnet build ClassroomToolkit.sln -c Debug` -> pass
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> pass (3200 passed)
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> pass (25 passed)
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `[hotspot] status=PASS`
- 说明：
  - 第三轮一次并行执行时出现 `CS2012`（`obj` 文件被 `VBCSCompiler` 占用）为执行时竞争；已串行重跑 contract/invariant 并通过，不属于代码缺陷。
- 澄清协议留痕更新：
  - `issue_id`: rollcall-minimize-hide-unification-20260409
  - `attempt_count`: 3
  - `clarification_mode`: direct_fix
  - `clarification_scenario`: bugfix
  - `clarification_questions`: none
  - `clarification_answers`: none

---

## 10) 第四轮增量修复（关键窗口严格前台）
- 触发原因：用户明确要求层级规则应为“启动器/悬浮球、工具条、点名及项目其它窗口始终高于 PDF/图片全屏与白板背景”，并反馈偶发掉层。
- 变更点：
  - `src/ClassroomToolkit.App/MainWindow.xaml.cs`
    - 在 `EnsureCriticalFloatingWindowsTopmost(...)` 中引入 `strictEnforceZOrder`：
      - 当关键窗口任一可见时，强制 `enforceZOrder = true`；
      - 对工具条、点名、启动器/悬浮球、点名附属覆盖层统一使用 `strictEnforceZOrder` 执行 `WindowTopmostExecutor.ApplyNoActivate(...)`。
- 验证（硬门禁）：
  - `dotnet build ClassroomToolkit.sln -c Debug` -> pass
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> pass (3200 passed)
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> pass (25 passed)
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `[hotspot] status=PASS`
- 说明：
  - 首次验证因运行中的 `sciman Classroom Toolkit.exe` 锁定输出文件导致失败；关闭进程后重跑完整门禁通过。
- 澄清协议留痕更新：
  - `issue_id`: rollcall-minimize-hide-unification-20260409
  - `attempt_count`: 4
  - `clarification_mode`: direct_fix
  - `clarification_scenario`: bugfix
  - `clarification_questions`: none
  - `clarification_answers`: none

---

## 11) 第五轮增量修复（照片顶部姓名延迟与闪烁体感）
- 触发原因：用户反馈“顶部姓名仍偶发明显延迟，照片偶发闪一下”。
- 根因判断：
  - `ShowPhoto` 起始阶段把姓名强制隐藏，导致姓名只能等到位图异步完成后才出现；
  - `ApplyLoadedBitmap` 中姓名布局应用通过 `DispatcherPriority.Background` 再次排队，进一步拉长可见时间；
  - `LoadingMask` 层级高于 `NameBadge`，即使姓名已写入也会被遮罩覆盖，形成“晚显示”体感。
- 变更点：
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
    - `ShowPhoto(...)` 中将 `UpdateStudentName(..., visible:false)` 调整为按姓名是否非空立即可见；
    - `ApplyLoadedBitmap(...)` 增加同步首帧 `ApplyOverlayLayoutAfterPhotoLoad()`，保留原有异步补偿用于后续布局稳定。
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
    - `LoadingMask` `Panel.ZIndex` 从 `100` 调整为 `8`，并设置 `IsHitTestVisible="False"`；
    - `NameBadge` `Panel.ZIndex` 调整为 `120`，确保加载遮罩不覆盖姓名徽标。
- 验证：
  - `dotnet build ClassroomToolkit.sln -c Debug` -> pass
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlay"` -> pass (19 passed)
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> pass (25 passed)
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `[hotspot] status=PASS`
- 执行说明：
  - 一次并行测试出现 `CS2012`（`VBCSCompiler` 文件锁）后已串行重跑对应测试并通过。
- 澄清协议留痕更新：
  - `issue_id`: rollcall-minimize-hide-unification-20260409
  - `attempt_count`: 5
  - `clarification_mode`: direct_fix
  - `clarification_scenario`: bugfix
  - `clarification_questions`: none
  - `clarification_answers`: none

---

## 12) 第六轮增量修复（点名设置窗口层级被点名窗口遮挡）
- 触发原因：用户反馈点名设置窗口打开后会被点名窗口遮挡。
- 根因判断：点名窗口处于强制 topmost 重排体系中，而点名页弹窗默认未统一进入同级保护，显示期间会被点名窗口回压。
- 变更点：
  - `src/ClassroomToolkit.App/RollCallWindow.xaml.cs`
    - `TryShowDialogSafe(...)` 增加统一弹窗层级保护：
      - 若未设置 Owner，默认绑定为点名窗口；
      - `dialog.ShowInTaskbar = false`；
      - `dialog.Topmost = true`；
      - 显示期间临时将点名窗口 `Topmost=false`，关闭后恢复；
      - 恢复后调用 `WindowTopmostExecutor.ApplyNoActivate(this, ..., enforceZOrder:true)` 重建点名窗口前台状态。
- 验证：
  - `dotnet build ClassroomToolkit.sln -c Debug` -> pass
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> pass (25 passed)
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> pass (3200 passed)
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `[hotspot] status=PASS`
- 执行说明：
  - 合约测试重跑时出现 `MSB3026`（`testhost` 持有测试 DLL）警告，自动重试后通过，不影响最终结果。
- 澄清协议留痕更新：
  - `issue_id`: rollcall-minimize-hide-unification-20260409
  - `attempt_count`: 6
  - `clarification_mode`: direct_fix
  - `clarification_scenario`: bugfix
  - `clarification_questions`: none
  - `clarification_answers`: none
