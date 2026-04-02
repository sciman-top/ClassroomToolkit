规则ID=R1,R2,R3,R4,R6,R8
影响模块=RollCall 屏幕中央学生大照片叠加窗
当前落点=src/ClassroomToolkit.App/RollCallWindow.Photo.cs
目标归宿=在 RollCallWindow 层对“不同学生”的中央照片显示切换执行旧叠加窗销毁与新叠加窗重建，阻断旧合成帧复显
迁移批次=2026-04-03-batch-rollcall-central-photo-overlay
风险等级=Low
执行命令=codex status; codex --version; codex --help; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --nologo -m:1 --filter "FullyQualifiedName~RollCallWindowPhotoOverlayReuseContractTests"; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --nologo -m:1 --filter "FullyQualifiedName~RollCallWindowPhotoOverlayReuseContractTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests|FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests"; dotnet build ClassroomToolkit.sln -c Debug --no-restore -m:1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --no-build --nologo -m:1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --no-build --nologo -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=新增回归契约先红后绿；build 0 error；全量测试 3113/3113 通过；contract/invariant 24/24 通过；hotspot PASS
回滚动作=回滚 src/ClassroomToolkit.App/RollCallWindow.Photo.cs 与 tests/ClassroomToolkit.Tests/RollCallWindowPhotoOverlayReuseContractTests.cs，并按同序复跑门禁

## Root Cause

中央大照片不是右侧小预览链路，而是 `RollCallWindow -> PhotoOverlayWindow`。

现有实现会在不同学生之间复用同一个 `PhotoOverlayWindow`：

1. 旧窗口被 `Hide()`，但窗口合成帧仍可能被系统保留
2. 新学生照片再次进入同一个透明叠加窗时，即使已有 `Opacity/LoadingMask` 防护，仍可能在复显首帧看到上一名学生的照片
3. 因此根因不是“路径没更新”，而是“不同学生共用同一个中央叠加窗”

## Changes

1. `src/ClassroomToolkit.App/RollCallWindow.Photo.cs`
   - 在 `UpdatePhotoDisplay` 中新增不同学生切换判定：
     - 当 `_photoOverlay != null`
     - 且 `_lastPhotoStudentId` 非空
     - 且 `!string.Equals(_lastPhotoStudentId, studentId, StringComparison.OrdinalIgnoreCase)`
   - 先执行 `ClosePhotoOverlay();`
   - 然后再 `EnsurePhotoOverlay()` 创建新窗口并显示当前学生照片
   - 仅在 `ShowPhoto(...)` 成功进入显示链路后再更新 `_lastPhotoStudentId`
2. `tests/ClassroomToolkit.Tests/RollCallWindowPhotoOverlayReuseContractTests.cs`
   - 新增契约：不同学生切换时，`UpdatePhotoDisplay` 必须先关闭旧叠加窗，再创建新叠加窗

## Red/Green Evidence

1. RED
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --nologo -m:1 --filter "FullyQualifiedName~RollCallWindowPhotoOverlayReuseContractTests"`
   - result: fail
   - 失败点：`UpdatePhotoDisplay` 中不存在 `ClosePhotoOverlay();`
2. GREEN
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --nologo -m:1 --filter "FullyQualifiedName~RollCallWindowPhotoOverlayReuseContractTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests|FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests"`
   - result: pass（`5/5`）

## Gate Evidence

1. `dotnet build ClassroomToolkit.sln -c Debug --no-restore -m:1`
   - result: pass（0 warning, 0 error）
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --no-build --nologo -m:1`
   - result: pass（`3113/3113`）
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --no-build --nologo -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: pass（`24/24`）
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - result: pass

## platform_na

- type: platform_na
- cmd: codex status
- exit_code: 1
- reason: 非交互终端执行，返回 `stdin is not a terminal`
- alternative_verification: 通过 `codex --version` 与 `codex --help` 完成 CLI 能力确认；active_rule_path 采用仓库根 AGENTS.md 与 GlobalUser/AGENTS.md 语义承接
- evidence_link: docs/change-evidence/20260403-rollcall-central-photo-overlay-previous-frame-flash.md
- expires_at: 2026-04-30
