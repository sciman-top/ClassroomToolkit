规则ID=R2,R4,R6,R8,E4,E5
影响模块=scripts/quality/run-local-quality-gates.ps1; tests/ClassroomToolkit.Tests
当前落点=D:\CODE\ClassroomToolkit
目标归宿=本仓质量门禁、性能门禁与测试证据
迁移批次=20260502-systematic-review-quality-and-performance-gates
风险等级=低：仅调整本地质量脚本失败路径与测试门禁采样方式；不改变生产功能、接口、数据格式或课堂 UI 行为。
执行命令=
- baseline: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- baseline: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3471 passed
- baseline: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -> 28 passed
- baseline: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1 -> PASS
- baseline: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS
- targeted: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RunLocalQualityGatesProfilePropagationContractTests" -> 2 passed
- targeted: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~BrushPerformanceGuardTests" -> 8 passed
- targeted: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~BrushPerformanceGuardTests" -> 8 passed
- follow-up: run-local-quality-gates stable-tests exposed residual BrushPerformanceGuardTests SpiralLoop ratio 1.3528801245459263 > 1.35 after independent medians.
- follow-up: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~BrushPerformanceGuardTests" -> 8 passed
- follow-up: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~BrushPerformanceGuardTests" -> 8 passed
- follow-up: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~BrushPerformanceGuardTests" -> 8 passed
- final: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3472 passed
- final: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" -> 28 passed
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1 -> PASS
- final: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS
- follow-up final after paired-ratio stabilization: dotnet build ClassroomToolkit.sln -c Debug -> 0 warning / 0 error
- follow-up final after paired-ratio stabilization: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -> 3473 passed
- follow-up final after paired-ratio stabilization: contract filter -> 28 passed
- follow-up final after paired-ratio stabilization: hotspot -> PASS
- follow-up final after paired-ratio stabilization: run-local-quality-gates.ps1 -Profile standard -Configuration Debug -> ALL PASS
验证证据=
- 质量脚本失败重试的文件锁文本匹配改为 IndexOf(..., StringComparison.OrdinalIgnoreCase)，避免 Windows PowerShell 5.1 失败路径不支持 string.Contains(string, StringComparison)。
- 新增 RunLocalQualityGatesProfilePropagationContractTests.RetryDetection_ShouldRemainCompatible_WithWindowsPowerShell，锁定脚本兼容性边界。
- 首次 final quality run 暴露 BrushPerformanceGuardTests 偶发比值 1.371 > 1.35；单测复跑通过，判定为微基准采样噪声。
- BrushPerformanceGuardTests 改为 baseline/candidate 分别采样、交替测量顺序、以两个中位数求相对成本；性能预算阈值不放宽。
- 后续 residual 修复：BrushPerformanceGuardTests 改为每轮 baseline/candidate 成对测量并取 paired ratio 中位数，同时将每轮 passes 从 3 提升到 5、iterations 从 7 提升到 9；性能预算阈值仍不放宽。
- artifacts/TestResults/stable-tests-summary.json: exit_code=0, duration_ms=11239, command=dotnet test ... -m:1。
- artifacts/quality/analyzer-backlog-report.json: diagnostics_total=95, rule=CA1515, count=95。
- dependency-vulnerability: PASS no vulnerable packages detected。
- dependency-governance: stable outdated packages are covered by active waivers。
回滚动作=
- git checkout -- scripts/quality/run-local-quality-gates.ps1 tests/ClassroomToolkit.Tests/BrushPerformanceGuardTests.cs tests/ClassroomToolkit.Tests/RunLocalQualityGatesProfilePropagationContractTests.cs docs/change-evidence/20260502-systematic-review-quality-and-performance-gates.md
- 回滚后重新执行固定门禁：build -> test -> contract/invariant -> hotspot，并补跑 run-local-quality-gates.ps1。
