规则ID=R1,R2,R4,R6,R8
影响模块=src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs
当前落点=点名窗口启动链路中的预热/加载衔接（RollCallViewModel）
目标归宿=复用同一份 students.xlsx 的预热任务，避免并行重复读取
迁移批次=20260401-rollcall-preload-dedup
风险等级=低
执行命令=codex status; codex --version; codex --help; Get-Command dotnet; Get-Command powershell; Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build/test/contract/hotspot 全部通过；首开路径由“预热+兜底二次Load”改为“优先等待同源预热任务完成”；active_rule_path=E:/CODE/ClassroomToolkit/AGENTS.md (source=project-doc)
回滚动作=git checkout -- src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs
platform_na=cmd: codex status | reason: stdin is not a terminal（非交互终端限制） | alternative_verification: codex --version + codex --help + active_rule_path 记录 | evidence_link: docs/change-evidence/20260401-rollcall-preload-dedup.md | expires_at: 2026-05-01

补充变更=主窗口启动时预创建并预热 RollCallWindow（MainWindow.WarmupRollCallData）；RollCallWindow 新增 WarmupData()；ViewModel 增加 IsDataReady 防止窗口首显重复加载。
补充验证=dotnet build 通过；dotnet test 全量 3048/3048 通过；contract/invariant 24/24 通过；hotspot PASS。
说明=一次并行执行导致 App.dll 文件锁（CS2012），已按串行重跑门禁并通过。

补充变更2=修复连页+记忆语义冲突：记忆关闭时禁止 unified transform 运行时应用与持久化；关闭记忆时清空页级 transform 缓存。
补充测试2=PhotoUnifiedTransformApplyPolicyTests 扩展为 remember/crosspage/photoInk 8 组合；全量测试 3052/3052 通过。
风险备注=并行执行时偶发 CS2012（App.dll 文件锁），串行重跑门禁通过。
