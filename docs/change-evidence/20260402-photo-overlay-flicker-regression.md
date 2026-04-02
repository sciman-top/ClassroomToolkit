# 2026-04-02 学生照片切换旧帧闪现回归修复

- rule_id: `R1/R2/R4/R6/R8`
- risk_level: `low`
- scope: `RollCall 学生照片叠加层`
- current_destination: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
- target_destination: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`（根因归宿）
- rollback_action:
  1. 还原 `PhotoOverlayWindow.xaml` 中 `LoadingMask` 背景改动
  2. 还原 `OverlayWindowsXamlContractTests` 对 `LoadingMask` 的契约断言

## Root Cause

`LoadingMask` 在代码路径中会被设为 `Visible`，但其背景为 `Transparent`。  
在窗口复显/切图瞬间，透明遮挡层无法覆盖旧帧，导致用户可见“前一张先闪一下”。

## Changes

1. `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
   - `LoadingMask.Background`: `Transparent` -> `{StaticResource Brush_OverlayMask}`
2. `tests/ClassroomToolkit.Tests/App/OverlayWindowsXamlContractTests.cs`
   - 新增契约断言：`LoadingMask` 必须使用 `Brush_OverlayMask`，防止回归

## Commands & Evidence

1. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: pass
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: pass (`3106` passed)
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: pass (`24` passed)
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: pass

## N/A Record

- type: `platform_na`
- cmd: `codex status`
- reason: 非交互终端返回 `stdin is not a terminal`
- alternative_verification:
  1. `codex --version`（成功，`codex-cli 0.118.0`）
  2. `codex --help`（成功，命令帮助可用）
- evidence_link: `docs/change-evidence/20260402-photo-overlay-flicker-regression.md`
- expires_at: `2026-04-09`

## Round 2（用户复现后追加修复）

- 触发时间: `2026-04-02`
- 新增风险点:
  1. 仅依赖遮挡层仍可能在窗口复显首帧看到历史内容
  2. 历史配置使用旧节名 `[RollCall]` 时，加载链路未兼容，点名设置回落默认

### 追加 Root Cause

1. `PhotoOverlayWindow.ShowPhoto` 在窗口可见前未做窗口级透明保护；在特定系统复显路径下可能先看到旧帧缓存。  
2. `AppSettingsService.Load` 仅读取 `RollCallTimer` 节，未兼容旧节名 `RollCall`，导致每次读取为默认值并在后续保存时覆盖用户配置。

### 追加 Changes

1. `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
   - 新增加载期窗口透明保护：新图加载前 `Opacity = 0.0`，新图应用后 `Opacity = 1.0`
   - 同图复用与关闭路径统一恢复 `Opacity = 1.0`
2. `src/ClassroomToolkit.App/Settings/AppSettingsService.cs`
   - 新增 `TryGetRollCallSection(...)`
   - 加载时优先 `RollCallTimer`，回退兼容 `RollCall`
3. `tests/ClassroomToolkit.Tests/PhotoOverlayShowOrderContractTests.cs`
   - 新增契约：显示前必须先设置 `Opacity = 0.0`，并在加载后恢复
4. `tests/ClassroomToolkit.Tests/AppSettingsServiceTests.cs`
   - 新增兼容测试：`[RollCall]` 节可正确加载点名配置

### 追加 Verification

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayShowOrderContractTests|FullyQualifiedName~AppSettingsServiceTests"`
   - result: pass (`26` passed)
2. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: pass
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: pass (`3108` passed)
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: pass (`24` passed)
5. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: pass

## Round 3（本次追加：复显首帧透明保护补强）

- 触发时间: `2026-04-02`
- 当前落点: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
- 目标归宿: `PhotoOverlayWindow.ShowPhoto` 复显路径透明保护（根因归宿）
- 迁移批次: `20260402-photo-overlay-flicker-round3`

### 追加 Root Cause

`ShowPhoto` 在 `EnsureOverlayVisible()` 之前设置了 `Opacity = 0.0`，但在部分窗口复显路径中，首帧仍可能复用上一轮合成内容。  
缺少“窗口可见后再次透明保护”的二次防线，导致旧帧偶发瞬时可见。

### 追加 Changes

1. `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
   - 在非同图切换分支中，`EnsureOverlayVisible()` 后再次执行 `Opacity = 0.0`。
   - 保持现有加载完成恢复 `Opacity = 1.0` 逻辑不变。
2. `tests/ClassroomToolkit.Tests/PhotoOverlayShowOrderContractTests.cs`
   - 新增契约测试：`ShowPhoto_ShouldReapplyTransparentGuard_AfterOverlayBecomesVisible`。
   - 约束 `EnsureOverlayVisible()` 之后必须存在二次透明保护。

### Red/Green Evidence

1. RED（先失败）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayShowOrderContractTests"`
   - result: fail（新增契约未满足）
2. GREEN（修复后）
   - 同命令重跑
   - result: pass（`3/3`）

### Gate Evidence（固定顺序）

1. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: pass（0 error）
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: pass（`3109` passed）
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: pass（`24` passed）
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: pass

### Platform Diagnostics（B.2 留痕）

- timestamp: `2026-04-02 23:46:19 +08:00`
- cmd: `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.118.0`
- cmd: `codex --help`
  - exit_code: `0`
  - key_output: `Codex CLI ... Commands: exec/review/login/...`

## Round 4（用户复现“还是闪”后追加：点名链路照片路径刷新）

- 触发时间: `2026-04-02`
- 当前落点: `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Navigation.cs`
- 目标归宿: `TryRollNext` 后同步刷新 `CurrentStudentPhotoPath`（根因归宿）
- 迁移批次: `20260402-rollcall-photo-path-refresh`

### 追加 Root Cause

`RollCallViewModel.TryRollNext` 仅更新了 `CurrentStudentId/CurrentStudentName`，未同步更新 `CurrentStudentPhotoPath`。  
点名窗口右侧照片绑定持续引用上一名学生路径，视觉上表现为“先看到前一个照片再切到当前”。

### 追加 Changes

1. `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Navigation.cs`
   - 在 `TryRollNext` 选中学生后，立即按当前班级规则刷新 `CurrentStudentPhotoPath`。
2. `tests/ClassroomToolkit.Tests/RollCallViewModelPhotoPathRefreshTests.cs`
   - 新增回归测试：`TryRollNext_ShouldRefreshCurrentStudentPhotoPath_ForRolledStudent`。
   - 约束：点名后 `CurrentStudentPhotoPath` 必须指向当前学生照片，不可沿用旧值。

### Red/Green Evidence

1. RED（先失败）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests"`
   - result: fail（期望当前学生照片路径，实际为 `null`）
2. GREEN（修复后）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests"`
   - result: pass（`4/4`）

### Gate Evidence（固定顺序，Round 4）

1. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: pass（0 error）
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: pass（`3110` passed）
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: pass（`24` passed）
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: pass

## Round 5（用户追问“已清缓存为何仍闪”后追加：Hide 前合成帧防护）

- 触发时间: `2026-04-02`
- 当前落点: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
- 目标归宿: `CloseOverlay` 隐藏前进入透明遮挡态，阻断旧合成帧复显
- 迁移批次: `20260402-photo-overlay-close-hide-guard`

### 追加 Root Cause

即使 `PhotoImage.Source = null`，这只是“逻辑图源/业务缓存”层清理；  
窗口管理器仍可能在 `Hide -> Show` 之间复用上一次窗口合成帧。  
原实现在 `Hide()` 前恢复 `Opacity=1` 且关闭遮挡层，给了旧帧复显机会。

### 追加 Changes

1. `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
   - `CloseOverlay`：改为先 `ClearPhotoCache(enterHideGuardState: true)`，再强制 `LoadingMask.Visible + Opacity=0.0`，最后 `Hide()`。
   - `ClearPhotoCache`：新增参数 `enterHideGuardState`，区分“关闭隐藏防闪态”与“正常清理态”。
   - `OnOverlayClosed`：改为 `enterHideGuardState: false`，避免关闭窗口后残留遮挡态。
2. `tests/ClassroomToolkit.Tests/PhotoOverlayCloseHideGuardContractTests.cs`
   - 新增契约：`CloseOverlay` 必须在 `Hide()` 前进入 `LoadingMask.Visible + Opacity=0.0`。

### Red/Green Evidence

1. RED（先失败）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests"`
   - result: fail（旧实现不满足隐藏前透明遮挡）
2. GREEN（修复后）
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests|FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests"`
   - result: pass（`5/5`）

### Gate Evidence（固定顺序，Round 5）

1. `dotnet build ClassroomToolkit.sln -c Debug`
   - result: pass（0 error）
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: pass（`3111` passed）
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: pass（`24` passed）
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: pass

# Backfill 2026-04-03
当前落点=BACKFILL-2026-04-03
风险等级=BACKFILL-2026-04-03
规则ID=BACKFILL-2026-04-03
回滚动作=BACKFILL-2026-04-03
目标归宿=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
