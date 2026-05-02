规则ID=R2,R4,R6,R7,R8,E4,E5
影响模块=src/ClassroomToolkit.App; scripts/quality/analyzer-backlog-baseline.json
当前落点=D:\CODE\ClassroomToolkit
目标归宿=本仓 App 内部实现可见性与 analyzer backlog 收敛
迁移批次=20260502-analyzer-ca1515-internal-visibility-ratchet
风险等级=低：仅将已验证不属于外部契约的 App 内部实现类型从 public 收窄为 internal，并把 analyzer backlog baseline 收紧到当前扫描结果；不修改课堂 UI 行为、持久化格式、public constructor 链或外部数据契约。

执行命令=
- pre-change: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=95
- batch1: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- batch1: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=88
- batch2: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- batch2: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=84
- ratchet: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=84
- final: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3472 passed
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -> 28 passed
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1 -> PASS
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS, analyzer-backlog PASS total=84
- final: git diff --check -> exit 0; only CRLF normalization warnings

验证证据=
- CA1515 backlog 从 95 降到 84；剩余项集中在 WPF/XAML 类型、public constructor 参数、事件载荷、持久化 DTO 或窗口/服务公开边界，未在本批强行收窄。
- build 在两批可见性调整后均通过，未出现 CS0051 inconsistent accessibility。
- scripts/quality/analyzer-backlog-baseline.json 已从旧 total=637 收紧到 total=84，仅允许当前 App/CA1515 backlog 不回升。
- 完整本地质量门禁通过，包含 build、stable-tests、contract、hotspot、governance-truth-source、dependency-governance、dependency-vulnerability、logging-alert-threshold、analyzer-backlog-baseline。

回滚动作=
- git checkout -- src/ClassroomToolkit.App/Diagnostics/BorderBrushDiagnostic.cs src/ClassroomToolkit.App/Diagnostics/DiagnosticsBundleExportService.cs src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs src/ClassroomToolkit.App/Ink/InkFinalCache.cs src/ClassroomToolkit.App/Ink/InkGeometrySerializer.cs src/ClassroomToolkit.App/Ink/InkHistoryPersistenceBridge.cs src/ClassroomToolkit.App/Ink/InkStorageService.cs src/ClassroomToolkit.App/Ink/InkStrokeRenderer.cs src/ClassroomToolkit.App/Ink/InkWriteAheadLogService.cs src/ClassroomToolkit.App/Utilities/CustomCursors.cs scripts/quality/analyzer-backlog-baseline.json docs/change-evidence/20260502-analyzer-ca1515-internal-visibility-ratchet.md
- 回滚后重新执行固定门禁：build -> test -> contract/invariant -> hotspot，并补跑 analyzer-backlog-baseline。
