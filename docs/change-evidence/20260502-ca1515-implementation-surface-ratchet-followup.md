规则ID=R2,R4,R6,R7,R8,E4
影响模块=src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs; src/ClassroomToolkit.App/Windowing/WindowOrchestrator.cs; src/ClassroomToolkit.App/Ink/InkExportService.cs; src/ClassroomToolkit.App/GlobalSuppressions.cs
当前落点=D:\CODE\ClassroomToolkit
目标归宿=App 内部实现面继续收窄，降低 CA1515 analyzer backlog
迁移批次=20260502-ca1515-implementation-surface-ratchet-followup
风险等级=低：仅将 DI 内部实现类和内部详细导出结果类型收窄为 internal；公开接口、公开简化导出 API、XAML 类型、持久化 DTO 与设置格式保持不变。

执行命令=
- pre-change: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=84
- implementation-surface: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- implementation-surface: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=82
- detailed-export-result: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- detailed-export-result: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=81
- note: 一次并行运行 build 与 analyzer 时触发 WPF obj/ref 生成文件竞争，build 报 CS0006/CS2001；串行重跑通过，按宿主构建状态竞争处理，不作为产品回归。
- final: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3473 passed
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -> 28 passed
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1 -> PASS
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> first run hit one BrushPerformanceGuardTests CornerZigZag microbenchmark ratio failure; targeted 5-run reproduction passed; rerun standard gate -> ALL PASS, analyzer-backlog PASS total=81

验证证据=
- CA1515 backlog 从 84 降到 81。
- PaintWindowOrchestrator 与 WindowOrchestrator 仍通过 public interface 注册和消费，具体实现不再暴露为 public。
- InkExportService.ExportAllPagesForFile 保留 public 行为；ExportAllPagesForFileDetailed 与 InkExportRunResult 仅供 App 内部和 friend test assembly 使用。
- 删除 InkExportRunResult 可见 nested type 抑制项后，analyzer baseline 仍通过并降到 total=81。
- 剩余 CA1515 项暂不继续自动收窄，主要涉及 WPF/XAML 类型、public constructor 边界、事件/状态公开载荷、ViewModel 绑定类型、持久化 DTO 或设置结构，需要单独兼容性审查。
- BrushPerformanceGuardTests 单次失败未能复现：同类定向重跑、`-m:1 --no-build` 定向重跑和 5 连跑均通过；随后 standard quality gate 全链通过，按一次性微基准采样噪声记录，不修改性能预算阈值。

回滚动作=
- git checkout -- src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs src/ClassroomToolkit.App/Windowing/WindowOrchestrator.cs src/ClassroomToolkit.App/Ink/InkExportService.cs src/ClassroomToolkit.App/GlobalSuppressions.cs docs/change-evidence/20260502-ca1515-implementation-surface-ratchet-followup.md
- 回滚后重新执行固定门禁：build -> test -> contract/invariant -> hotspot，并补跑 analyzer-backlog-baseline。
