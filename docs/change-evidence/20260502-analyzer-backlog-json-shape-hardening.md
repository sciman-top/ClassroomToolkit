规则ID=R2,R4,R6,R8,E4
影响模块=scripts/quality/check-analyzer-backlog-baseline.ps1; scripts/quality/analyzer-backlog-baseline.json
当前落点=D:\CODE\ClassroomToolkit
目标归宿=analyzer backlog 报告与 baseline JSON 形态稳定
迁移批次=20260502-analyzer-backlog-json-shape-hardening
风险等级=低：仅加固质量脚本的 JSON 生成/读取形态，不改变扫描范围、规则阈值或生产代码行为。

执行命令=
- targeted: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug -> PASS total=84
- targeted: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~GovernanceTruthSourceContractTests" -> 5 passed
- targeted: parallel analyzer/test attempt -> analyzer PASS total=84; test failed with transient CS0006 missing ref dll while analyzer build was running; rerun serially passed.
- final: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3473 passed
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -> 28 passed
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1 -> PASS
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS, stable-tests 3473 passed, analyzer-backlog PASS total=84
- follow-up final after LiteralPath guard: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- follow-up final after LiteralPath guard: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3473 passed
- follow-up final after LiteralPath guard: contract filter -> 28 passed
- follow-up final after LiteralPath guard: hotspot -> PASS
- follow-up final after LiteralPath guard: run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS

验证证据=
- artifacts/quality/analyzer-backlog-report.json 在仅剩一个 project/rule 时仍输出 project_counts/rule_counts 数组。
- baseline 读取端使用 @($baseline.rule_counts) 与 @($baseline.project_counts)，兼容对象或数组形态。
- analyzer 脚本使用 Get-ChildItem/Get-Content/Set-Content 的 -LiteralPath 形式，避免路径通配符解析。
- scripts/quality/analyzer-backlog-baseline.json 与最新 report 时间戳和 total=84 对齐。
- GovernanceTruthSourceContractTests 覆盖脚本数组包裹、baseline 读取兼容、LiteralPath 路径访问与 baseline JSON 数组形态。
- 完整 quality gate 通过，覆盖 build、stable-tests、contract、hotspot、governance、dependency 与 analyzer。

回滚动作=
- git checkout -- scripts/quality/check-analyzer-backlog-baseline.ps1 scripts/quality/analyzer-backlog-baseline.json docs/change-evidence/20260502-analyzer-backlog-json-shape-hardening.md
- 回滚后重新执行 analyzer-backlog-baseline 与固定门禁。
