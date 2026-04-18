# 2026-04-19 WPS Fullscreen Exit Ink Clear

## Goal
- rule_id: `R1/R2/R3/R6/R8`
- 边界：`src/ClassroomToolkit.App/Paint`
- 当前落点：`PaintOverlayWindow` 的演示全屏识别与退出清屏
- 目标归宿：让 `WPS` 放映进入时被正确记为 `presentation fullscreen`，退出时稳定触发 `ClearInkSurfaceForPresentationExit()`

## Trace
- issue_id: `wps-fullscreen-exit-ink-clear`
- attempt_count: `2`
- clarification_mode: `direct_fix`
- clarification_scenario: `bugfix`
- clarification_questions: `[]`
- clarification_answers: `[]`

## Basis
- 用户现象：`PowerPoint` 退出放映会自动清空笔迹，`WPS` 不会。
- 第一轮假设不成立：不是单纯“WPS 退出后后台残留 fullscreen candidate”。
- 现场窗口探针结果：
  - 放映前：`wps.exe + PP12FrameClass + fullscreen=false + admit=false`
  - 放映中：`wpp.exe + Qt5QWindowIcon + fullscreen=true + slideshow=false + dedicated=true + admit=true`
  - `Esc` 后：`wps.exe + PP12FrameClass + fullscreen=false + admit=false`
- 根因定位：
  - `IsFullscreenPresentationWindow()` 对 `WPS` 仅接受 `slideshow class match`。
  - 当前 WPS 版本的实际放映窗类名是通用 `Qt5QWindowIcon`，不在既有 `kwps/kwpp show class` 集合内。
  - 结果是：放映期间 `_presentationFullscreenActive` 从未进入 `true`，退出时也就不会产生 `true -> false` 的清屏转移。

## Changes
- `[PaintOverlayWindow.Presentation.cs](/D:/OneDrive/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs)`
  - 保留第一轮的 `WpsFullscreenExitPolicy` 前台修正。
  - 在 `IsFullscreenPresentationWindow()` 中新增 `dedicatedWpsRuntime` 判定。
- `[PresentationFullscreenWindowAdmissionPolicy.cs](/D:/OneDrive/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PresentationFullscreenWindowAdmissionPolicy.cs)`
  - 允许“全屏 + 专用 WPS 放映进程”在无 slideshow class 时仍被视为演示全屏。
- `[WpsPresentationRuntimePolicy.cs](/D:/OneDrive/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/WpsPresentationRuntimePolicy.cs)`
  - 新增纯策略，识别 `wpp/wppt/wpspresentation*` 这类专用 WPS 放映运行时。
- `[PresentationFullscreenWindowAdmissionPolicyTests.cs](/D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/PresentationFullscreenWindowAdmissionPolicyTests.cs)`
  - 新增“专用 WPS 放映进程可通过、普通 `wps.exe` 编辑窗不可通过”的回归覆盖。
- `[WpsPresentationRuntimePolicyTests.cs](/D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/WpsPresentationRuntimePolicyTests.cs)`
  - 新增运行时识别测试。
- `[WpsFullscreenExitPolicy.cs](/D:/OneDrive/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/WpsFullscreenExitPolicy.cs)`
  - 保留第一轮退出收敛逻辑，用于处理少量“退出后残留 fullscreen candidate”场景。
- `[WpsFullscreenExitPolicyTests.cs](/D:/OneDrive/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/WpsFullscreenExitPolicyTests.cs)`
  - 第一轮已有测试保留。

## Commands
- 诊断
  - `codex --version`
  - `codex --help`
  - `codex status`
- 现场探针
  - `PowerShell inline probe: AppActivate WPS -> SendKeys {F5}/{ESC} -> EnumWindows dump`
- 门禁
  - `dotnet build ClassroomToolkit.sln -c Debug -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin-agent\Debug\net10.0-windows\`
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin-agent\Debug\net10.0-windows\`
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -p:OutDir=D:\OneDrive\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin-agent\Debug\net10.0-windows\`

## Evidence
- `codex --version`
  - `exit_code=0`
  - `key_output=codex-cli 0.121.0`
- `codex --help`
  - `exit_code=0`
  - `key_output=Codex CLI help rendered`
- `现场探针`
  - `exit_code=0`
  - `key_output(before-start)=wps.exe + PP12FrameClass + fullscreen=false + admit=false`
  - `key_output(during-slideshow)=wpp.exe + Qt5QWindowIcon + fullscreen=true + dedicated=true + admit=true`
  - `key_output(after-esc)=wps.exe + PP12FrameClass + fullscreen=false + admit=false`
- `dotnet build ...`
  - `exit_code=0`
  - `timestamp=2026-04-19 02:32:52 +08:00`
  - `key_output=0 warning / 0 error`
- `dotnet test ...`
  - `exit_code=0`
  - `timestamp=2026-04-19 02:33:02 +08:00`
  - `key_output=Passed 3285/3285`
- `dotnet test ...contract/invariant...`
  - `exit_code=0`
  - `timestamp=2026-04-19 02:33:17 +08:00`
  - `key_output=Passed 28/28`
- 热点人工复核
  - 结论：
    - `WPS` 只对 `wpp/wppt/wpspresentation*` 这类专用运行时放宽，无额外放大到普通 `wps.exe` 编辑窗。
    - `Office` 路径未改。
    - 第一轮 `WpsFullscreenExitPolicy` 与第二轮 dedicated runtime 识别互补，不互相覆盖。

## Platform / Environment Notes
- `platform_na`
  - `reason=codex status` 在当前非交互终端返回 `stdin is not a terminal`
  - `alternative_verification=使用会话内 AGENTS 上下文 + codex --version/codex --help 作为平台诊断补证`
  - `evidence_link=this-file#evidence`
  - `expires_at=2026-04-19`
- 默认 `bin/Debug` 被运行中的 `sciman Classroom Toolkit` 占用；本次继续把门禁输出重定向到 `tests/ClassroomToolkit.Tests/bin-agent/Debug/net10.0-windows/`，门禁顺序未变化。

## Risk
- 风险等级：低
- 影响面：`WPS` 放映全屏识别与退出清屏
- 兼容性：
  - 未修改 `PowerPoint` 判定
  - 未改变笔迹存储格式
  - 普通 `wps.exe` 编辑窗仍不会被识别为演示全屏

## Rollback
- 回滚文件：
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
  - `src/ClassroomToolkit.App/Paint/PresentationFullscreenWindowAdmissionPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/WpsPresentationRuntimePolicy.cs`
  - `src/ClassroomToolkit.App/Paint/WpsFullscreenExitPolicy.cs`
  - `tests/ClassroomToolkit.Tests/PresentationFullscreenWindowAdmissionPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/WpsPresentationRuntimePolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/WpsFullscreenExitPolicyTests.cs`
- 回滚动作：
  - 撤销上述文件的本次变更后，按同一门禁顺序重新执行 `build -> test -> contract/invariant -> hotspot`
