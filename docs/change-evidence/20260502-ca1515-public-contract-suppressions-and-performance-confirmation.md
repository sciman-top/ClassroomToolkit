规则ID=R2,R4,R6,R7,R8,E4,E6
影响模块=src/ClassroomToolkit.App/GlobalSuppressions.cs; tests/ClassroomToolkit.Tests/BrushPerformanceGuardTests.cs; tests/ClassroomToolkit.Tests/PublicContractVisibilitySuppressionContractTests.cs
当前落点=D:\CODE\ClassroomToolkit
目标归宿=为真实公共契约显式记录 CA1515 豁免，并把性能守卫从一次性采样噪声中解耦
迁移批次=20260502-ca1515-public-contract-suppressions-and-performance-confirmation
风险等级=低：不改变生产行为、公开 API 形状、XAML 绑定、settings/ink 持久化格式或性能预算阈值；仅收紧分析器语义与测试判定稳定性。

执行命令=
- pre-change: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=81
- targeted: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PublicContractVisibilitySuppressionContractTests|FullyQualifiedName~GovernanceTruthSourceContractTests" -> 6 passed
- build note: 首次 build 命中 WPF 临时工程/XAML 生成态异常（`ClassroomToolkit.App_*_wpftmp.csproj`, missing InitializeComponent）；执行 `dotnet build-server shutdown` 后重跑 build 通过，按宿主构建状态竞争处理
- targeted: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=57
- targeted: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~BrushPerformanceGuardTests" -> 8 passed
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3474 passed
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -> 28 passed
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1 -> PASS
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS, analyzer-backlog PASS total=57

验证证据=
- CA1515 backlog 从 81 降到 57，净减少 24；减少项来自显式标注以下真实 public contract：
  settings 配置契约、ink sidecar/export DTO、presentation/windowing 事件载荷、ui session 状态/过渡模型。
- 新增 PublicContractVisibilitySuppressionContractTests，锁定这些 suppression 不是随手添加，而是仓库明确承认的兼容边界。
- BrushPerformanceGuardTests 不再因单次窄幅超阈值直接 fail；首次超阈值时会做一次完整确认采样，只有可复现超阈值才阻断。
- 性能预算阈值未放宽：Marker 仍为 1.35，Calligraphy 仍为 1.45。
- full test 计数从 3473 增至 3474，新增的是 PublicContractVisibilitySuppressionContractTests。
- 仍保留的 CA1515 主要集中在 WPF/XAML 根类型、dialog/window、view model、converter/control、以及若干公开工厂/持久化服务边界，下一轮需要继续分类而不是盲目 internal 化。

回滚动作=
- git checkout -- src/ClassroomToolkit.App/GlobalSuppressions.cs tests/ClassroomToolkit.Tests/BrushPerformanceGuardTests.cs tests/ClassroomToolkit.Tests/PublicContractVisibilitySuppressionContractTests.cs docs/change-evidence/20260502-ca1515-public-contract-suppressions-and-performance-confirmation.md
- 回滚后重新执行固定门禁：build -> test -> contract/invariant -> hotspot，并补跑 analyzer-backlog-baseline 与 standard quality gate。
