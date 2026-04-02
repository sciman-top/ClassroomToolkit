规则ID=R1,R2,R4,R6,R7,R8
影响模块=PaintSettingsDialog package UI wiring, MainWindow paint settings apply
当前落点=src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml; src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs; src/ClassroomToolkit.App/MainWindow.Paint.cs
目标归宿=在“场景与兼容”页支持演示识别规则包导入/导出（签名校验）并将导入覆盖写回设置
迁移批次=P1-3/3
风险等级=中
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1; codex status; codex --version; codex --help
验证证据=build 0 error; full test 3076/3076 pass; contract 24/24 pass; hotspot PASS; codex --version=codex-cli 0.118.0; codex --help printed usage
诊断.cmd.1=codex status
诊断.exit_code.1=1
诊断.key_output.1=Error: stdin is not a terminal
诊断.timestamp.1=2026-04-02
诊断.cmd.2=codex --version
诊断.exit_code.2=0
诊断.key_output.2=codex-cli 0.118.0
诊断.timestamp.2=2026-04-02
诊断.cmd.3=codex --help
诊断.exit_code.3=0
诊断.key_output.3=Codex CLI usage printed
诊断.timestamp.3=2026-04-02
platform_na=codex status non-interactive limitation
platform_na.reason=stdin is not a terminal
platform_na.alternative_verification=使用 codex --version 与 codex --help 验证 CLI 可用性
platform_na.evidence_link=docs/change-evidence/20260402-presentation-overrides-package-ui-wiring.md
platform_na.expires_at=2026-04-09
回滚动作=git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs src/ClassroomToolkit.App/MainWindow.Paint.cs docs/change-evidence/20260402-presentation-overrides-package-ui-wiring.md
