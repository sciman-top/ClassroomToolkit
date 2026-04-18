# 2026-04-19 ImageManager Add Favorite Dialog Z-Order

## Goal
- 边界：`src/ClassroomToolkit.App/Photos`
- 当前落点：`ImageManagerWindow` 的“添加收藏”文件夹浏览弹窗
- 目标归宿：弹出浏览窗口后不被资源管理窗口迅速盖住，浏览窗口保持前台可交互

## Basis
- 用户现象：点击“添加收藏”后浏览窗口出现但很快被资源管理窗口盖住。
- 根因定位：
  - `ImageManagerWindow` 固定 `Topmost=True`。
  - `FolderBrowserDialog.ShowDialog()` 未绑定 owner，且未进入 `FloatingTopmostDialogSuppressionState`。
  - 主窗口 z-order/watchdog 在此期间可能继续回抢，导致浏览窗口被覆盖。

## Changes
- `[ImageManagerWindow.Navigation.cs](/D:/OneDrive/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs)`
  - 为 `FolderBrowserDialog` 传入 `ImageManager` 句柄 owner（`IWin32Window` 包装）。
  - 打开浏览窗口前进入 `FloatingTopmostDialogSuppressionState.Enter()`。
  - 对话框期间临时下调 `ImageManagerWindow.Topmost=false`，关闭后恢复，并执行 `WindowTopmostExecutor.ApplyNoActivate(..., enforceZOrder: true)`。

## Commands
- `dotnet build ClassroomToolkit.sln -c Debug -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin-agent\Debug\net10.0-windows\`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin-agent\Debug\net10.0-windows\`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin-agent\Debug\net10.0-windows\`
- `codex --version`
- `codex --help`
- `codex status`

## Evidence
- `dotnet build ...`
  - `exit_code=0`
  - `timestamp=2026-04-19 02:47:05 +08:00`
  - `key_output=0 warning / 0 error`
- `dotnet test ...`
  - `exit_code=0`
  - `timestamp=2026-04-19 02:47:31 +08:00`
  - `key_output=Passed 3285/3285`
- `dotnet test ...contract/invariant...`
  - `exit_code=0`
  - `timestamp=2026-04-19 02:47:31 +08:00`
  - `key_output=Passed 28/28`
- `codex --version`
  - `exit_code=0`
  - `key_output=codex-cli 0.121.0`
- `codex --help`
  - `exit_code=0`
  - `key_output=Codex CLI help rendered`
- 热点人工复核
  - 结论：修复仅收敛于“添加收藏”对话框打开窗口期；未改动文件扫描、收藏/最近列表业务逻辑。

## N/A
- `platform_na`
  - `reason=codex status` 在非交互终端返回 `stdin is not a terminal`
  - `alternative_verification=以 AGENTS 加载链 + codex --version/codex --help + 本地门禁结果补证`
  - `evidence_link=this-file#evidence`
  - `expires_at=2026-04-19`

## Risk
- 风险等级：低
- 影响面：`ImageManagerWindow` 的“添加收藏”对话框打开期顶层行为
- 兼容性：不改变收藏数据结构与保存逻辑

## Rollback
- 回滚文件：
  - `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs`
- 回滚动作：
  - 撤销该文件本次变更后，按同一门禁顺序重跑 `build -> test -> contract/invariant -> hotspot`
