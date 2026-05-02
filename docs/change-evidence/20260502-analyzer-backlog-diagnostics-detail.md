规则ID=R2,R4,R6,R8,E4
影响模块=scripts/quality/check-analyzer-backlog-baseline.ps1; tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs
当前落点=D:\CODE\ClassroomToolkit
目标归宿=analyzer backlog 报告可直接定位剩余诊断
迁移批次=20260502-analyzer-backlog-diagnostics-detail
风险等级=低：仅增强质量脚本报告字段与合同测试，不改变扫描阈值、门禁顺序或生产代码行为。

执行命令=
- pre-change: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug -> ALL PASS, analyzer-backlog PASS total=84
- targeted: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=84
- targeted: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~GovernanceTruthSourceContractTests" -> 5 passed
- note: analyzer 与 dotnet test 并行尝试时触发 WPF obj 临时生成文件竞争，test 报 CS2001 missing StudentListDialog.g.cs；串行重跑通过，按本仓已知并行 build 风险处理。
- final: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3473 passed
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -> 28 passed
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1 -> PASS
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS, analyzer-backlog PASS total=84
- final: git diff --check -> exit 0; only CRLF normalization warnings

验证证据=
- analyzer backlog 脚本保留原 project_counts/rule_counts 计数，同时在 report 中输出 diagnostics 明细。
- diagnostics 使用仓库相对路径，避免把本机绝对路径写入可复用报告。
- GovernanceTruthSourceContractTests 锁定 diagnostics 明细、相对路径转换和既有数组形态。
- artifacts/quality/analyzer-backlog-report.json: diagnostics_total=84, diagnostics_count=84, first_file=src/ClassroomToolkit.App/AboutDialog.xaml.cs, all_relative=true。
- 完整 quality gate 覆盖 build、stable-tests、contract、hotspot、governance、dependency、vulnerability、logging threshold 与 analyzer baseline。

回滚动作=
- git checkout -- scripts/quality/check-analyzer-backlog-baseline.ps1 tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs docs/change-evidence/20260502-analyzer-backlog-diagnostics-detail.md
- 回滚后重新执行固定门禁：build -> test -> contract/invariant -> hotspot，并补跑 analyzer-backlog-baseline。
