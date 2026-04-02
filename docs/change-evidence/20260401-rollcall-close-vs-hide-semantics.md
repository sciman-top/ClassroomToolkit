规则ID=R1,R2,R4,R6,R7,R8
影响模块=src/ClassroomToolkit.App/RollCallWindow.xaml; src/ClassroomToolkit.App/RollCallWindow.Windowing.cs; tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs; tests/ClassroomToolkit.Tests/App/RollCallWindowLifecycleSubscriptionContractTests.cs
当前落点=点名窗口右上角仅有“关闭”动作且行为等同隐藏
目标归宿=区分“关闭(停止点名能力)”与“最小化/隐藏(点名能力持续运行)”
迁移批次=20260401-rollcall-close-vs-hide-semantics
风险等级=低
执行命令=codex status; codex --version; codex --help; Get-Command dotnet; Get-Command powershell; Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error；全量 test 3053/3053 pass；contract/invariant 24/24 pass（--no-build 回退复验）；hotspot PASS；active_rule_path=E:/CODE/ClassroomToolkit/AGENTS.md (source=project-doc)
回滚动作=git checkout -- src/ClassroomToolkit.App/RollCallWindow.xaml src/ClassroomToolkit.App/RollCallWindow.Windowing.cs tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs tests/ClassroomToolkit.Tests/App/RollCallWindowLifecycleSubscriptionContractTests.cs
platform_na=cmd: codex status | reason: stdin is not a terminal（非交互终端限制） | alternative_verification: codex --version + codex --help + active_rule_path 记录 | evidence_link: docs/change-evidence/20260401-rollcall-close-vs-hide-semantics.md | expires_at: 2026-05-01
gate_na=reason: contract/invariant 原命令触发 MSB3027/MSB3021（ClassroomToolkit.App.exe 被进程 ClassroomToolkit(62540) 锁定，无法复制 apphost） | alternative_verification: 先完成 build + full test，再以同 filter 执行 dotnet test --no-build，24/24 通过 | evidence_link: docs/change-evidence/20260401-rollcall-close-vs-hide-semantics.md | expires_at: 2026-04-08

补充变更=新增右上角“最小化”按钮（Icon_Minimize），行为与启动器“隐藏点名”一致；关闭按钮改为 RequestClose()，触发 OnClosing 清理链路并停止点名相关能力。
补充验证=UiCopyContractTests 新增“隐藏点名（功能继续）”文案约束；RollCallWindowLifecycleSubscriptionContractTests 新增“最小化/关闭分离语义”约束。
