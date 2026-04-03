规则ID=R1/R2/R4/R6/R8
影响模块=src/ClassroomToolkit.App/Ink, src/ClassroomToolkit.App/Paint, tests/ClassroomToolkit.Tests, scripts/quality
当前落点=Ink 全屏书写链路稳定性与诊断统一化
目标归宿=高频平移/缩放/跨页下笔迹稳定且可量化诊断
迁移批次=20260403-2
风险等级=中
执行命令=1) dotnet build ClassroomToolkit.sln -c Debug; 2) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; 3) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; 4) powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build PASS; full tests PASS(3167); contract/invariant PASS(25); hotspot PASS; 并行 file-lock 冲突已串行复验消除
回滚动作=按文内 Rollback 小节恢复改动文件并复跑 build->test->contract/invariant->hotspot

# Change Evidence — Ink Redraw Stability & Telemetry

- Date: 2026-04-03
- Scope: PDF/图片全屏书写链路（重绘、坐标、导出并发、诊断）
- Risk Level: Medium（渲染/输入路径变更，已做兼容回退）
- Rule IDs: R1, R2, R4, R6, R8

## 1) 依据（Basis）
- 问题背景：全屏书写链路存在“偶发错位、重绘压力、导出并发一致性、诊断出口分散”风险。
- 目标归宿：
  - 重绘：在不破坏现有行为前提下，引入安全局部清空路径。
  - 坐标：极小缩放下避免逆矩阵退化导致的 Identity 误映射。
  - 导出：并发导出时 manifest 不丢键。
  - 诊断：InkRedrawTelemetry 默认静默，显式开关后统一走 diagnostics。

## 2) 变更（Changes）
- 导出并发一致性：
  - `src/ClassroomToolkit.App/Ink/InkExportService.cs`
  - manifest 写入改为“按路径锁 + 磁盘合并”。
- 坐标稳态保护：
  - `src/ClassroomToolkit.App/Paint/PhotoInkCoordinateMapper.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
  - 新增 TryCreateInverseMatrix，运行时回退“最近有效逆矩阵”。
- 局部重绘（第一阶段）：
  - `src/ClassroomToolkit.App/Paint/InkRedrawClipPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Core.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.cs`
  - clip 稳定时局部 clear；clip 变化或不可判定时回退全量重绘。
- Telemetry 与统一出口：
  - `src/ClassroomToolkit.App/Paint/InkRedrawTelemetryPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/InkRuntimeDiagnostics.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.cs`
  - 新增环境开关：`CTK_INK_REDRAW_TELEMETRY`（`1/true/on/yes/enabled` 开启），默认关闭。
  - Telemetry 输出统一走 `InkRuntimeDiagnostics.OnInkRedrawTelemetry(...)`。
- 测试与契约：
  - `tests/ClassroomToolkit.Tests/InkExportServiceTests.cs`
  - `tests/ClassroomToolkit.Tests/PhotoInkCoordinateMapperTests.cs`
  - `tests/ClassroomToolkit.Tests/InkRedrawClipPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/InkRedrawTelemetryPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/PaintOverlayInkRedrawTelemetryContractTests.cs`
- 脚本契约修复：
  - `scripts/quality/run-local-quality-gates.ps1`
  - `stable-tests` 参数透传由 `-Profile quick` 修复为 `-Profile $Profile`。

## 3) 执行命令（Commands）
- Build:
  - `dotnet build ClassroomToolkit.sln -c Debug`
- Full tests:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- Contract/Invariant:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- Hotspot:
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 4) 验证证据（Evidence）
- Build: pass（0 error）
- Full tests: pass（最新一次：3167 passed）
- Contract/Invariant filter: pass（25 passed）
- Hotspot: pass（status=PASS）
- 说明：中途并行执行 `dotnet test` 时出现过 WPF/VBCSCompiler 文件锁冲突，已改为串行复跑并通过。

## 5) 回滚动作（Rollback）
- 代码回滚（仅本次链路）：
  - `git restore scripts/quality/run-local-quality-gates.ps1`
  - `git restore src/ClassroomToolkit.App/Ink/InkExportService.cs`
  - `git restore src/ClassroomToolkit.App/Paint/InkRuntimeDiagnostics.cs`
  - `git restore src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Core.cs`
  - `git restore src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.cs`
  - `git restore src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs`
  - `git restore src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
  - `git restore src/ClassroomToolkit.App/Paint/PhotoInkCoordinateMapper.cs`
  - `git clean -f src/ClassroomToolkit.App/Paint/InkRedrawClipPolicy.cs`
  - `git clean -f src/ClassroomToolkit.App/Paint/InkRedrawTelemetryPolicy.cs`
  - `git clean -f tests/ClassroomToolkit.Tests/InkRedrawClipPolicyTests.cs`
  - `git clean -f tests/ClassroomToolkit.Tests/InkRedrawTelemetryPolicyTests.cs`
  - `git clean -f tests/ClassroomToolkit.Tests/PaintOverlayInkRedrawTelemetryContractTests.cs`
- 回滚后复验：
  - 重新执行 `build -> test -> contract/invariant -> hotspot` 全链路。

## 6) N/A
- `platform_na`: N/A
- `gate_na`: N/A
