# 变更证据：PDF/图片全屏缩放卡顿与闪烁优化（2026-04-19）

## 规则与风险
- 规则 ID：R1/R2/R6/R8（先定归宿、小步闭环、门禁顺序、可追溯）
- 风险等级：中（涉及照片/PDF 交互渲染与跨页刷新调度）
- 目标归宿：`src/ClassroomToolkit.App/Paint/*` 的缩放交互渲染链路

## 变更摘要
- 缩放时取消 `CrossPageUpdateSources.WithImmediate(ApplyScale)` 的强制即时刷新，改为可节流请求 + 停稳后补刷。
- 新增交互渲染质量切换：交互中 `LowQuality`，停稳后恢复 `HighQuality`。
- 新建邻页图层时复用当前渲染质量模式，降低缩放/跨页时闪烁。
- 对邻页同步解析策略做收敛：
  - 交互中仅允许“未换槽位”的邻页位图同步解析；
  - PDF 交互中允许高度同步解析，图片序列保持异步路径。
- 第二轮（用户反馈后）进一步收敛缩放链路：
  - 缩放进行中不再触发实时跨页刷新，仅更新已有邻页 transform；
  - 缩放停稳后由渲染质量恢复计时器触发一次 `apply-scale-immediate`；
  - pinch 缩放期间屏蔽 `ManipulationDelta` 的跨页实时刷新请求，避免界线附近反复消隐。

## 命令与证据
- `codex --version`
  - `exit_code=0`
  - `key_output=codex-cli 0.121.0`
- `codex --help`
  - `exit_code=0`
  - `key_output=Codex CLI usage/help`
- `codex status`
  - `exit_code=1`
  - `platform_na`：
    - `reason=stdin is not a terminal（非交互环境）`
    - `alternative_verification=执行 codex --version 与 codex --help`
    - `evidence_link=本文件`
    - `expires_at=2026-04-26`

- `dotnet build ClassroomToolkit.sln -c Debug`
  - `exit_code=0`
  - `key_output=0 warnings, 0 errors`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - `exit_code=0`
  - `key_output=Passed 3285, Failed 0`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - `exit_code=0`
  - `key_output=Passed 28, Failed 0`

## Hotspot 人工复核
- 复核点 1：缩放交互期间不再强制 immediate 刷新，是否影响邻页同步。
  - 结论：已在缩放后追加 `ScheduleCrossPageDisplayUpdateAfterInputSettles`，并保留邻页 transform 同步，功能连续。
- 复核点 2：渲染质量切换是否泄漏到非照片模式。
  - 结论：进入/退出照片模式与窗口关闭路径均有 timer stop + 模式复位。
- 复核点 3：策略回归（邻页高度/位图同步解析）。
  - 结论：与现有单测期望一致，测试通过。

## 回滚入口
- 单文件回滚：`git checkout -- <file>`
- 本次核心文件回滚集合：
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.Zoom.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.RenderingQuality.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.Manipulation.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.Rendering.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.Seed.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.Mode.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.State.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Lifecycle.cs`
  - `src/ClassroomToolkit.App/Paint/CrossPageNeighborBitmapResolvePolicy.cs`
  - `src/ClassroomToolkit.App/Paint/CrossPageNeighborHeightResolvePolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PhotoTransformTimingDefaults.cs`
