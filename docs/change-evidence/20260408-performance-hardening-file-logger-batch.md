规则ID=R1,R2,R3,R6,R8
影响模块=src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs
当前落点=Infra logging queue writer
目标归宿=保留现有日志契约，改为批量落盘减少 I/O 抖动
迁移批次=2026-04-08-performance-hardening-batch1
风险等级=低
执行命令=
- codex status
- codex --version
- codex --help
- Get-Command dotnet
- Get-Command powershell
- Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~FileLoggerProviderShutdownSafetyContractTests"
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- FileLoggerProvider 定向测试通过：14 passed / 0 failed
- build 通过：0 errors / 0 warnings
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 代码差异：逐条 File.AppendAllText -> 批量 BuildQueueBatch + FlushBatch（按天分组）
回滚动作=
- git checkout -- src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs

platform_na=
- reason: codex status 在非交互终端失败，输出 stdin is not a terminal
- alternative_verification: 使用 codex --version 与 codex --help 补充平台信息，并执行仓库门禁命令完成运行态验证
- evidence_link: docs/change-evidence/20260408-performance-hardening-file-logger-batch.md
- expires_at: 2026-05-08
