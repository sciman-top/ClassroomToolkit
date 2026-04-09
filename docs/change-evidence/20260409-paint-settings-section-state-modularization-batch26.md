规则ID=R1,R2,R5,R6,R8
影响模块=
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.SectionState.cs
当前落点=PaintSettingsDialog.xaml.cs 末尾混合脏标记绑定、状态捕获/回填、默认值应用，单文件维护成本高
目标归宿=将 SectionState 相关方法组迁移到独立 partial，主文件保留入口与事件逻辑
迁移批次=2026-04-09-maintainability-hardening-batch26
风险等级=低
执行命令=
- codex status
- codex --version
- codex --help
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- platform_na: codex status 在非交互终端失败（stdin is not a terminal）
- codex --version 返回 codex-cli 0.118.0
- codex --help 正常输出命令帮助
- build 通过：0 errors / 0 warnings
- 全量测试通过：3199 passed / 0 failed
- contract/invariant 通过：25 passed / 0 failed
- hotspot 通过：status=PASS
- 热点体量下降：PaintSettingsDialog.xaml.cs 从 1062 降到 696（预算 1880）
N/A记录=
- type=platform_na
  reason=codex status 依赖交互终端，当前执行环境为非交互 shell
  alternative_verification=使用 codex --version 与 codex --help 补充平台诊断证据
  evidence_link=docs/change-evidence/20260409-paint-settings-section-state-modularization-batch26.md
  expires_at=2026-05-09
回滚动作=
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- git checkout -- src/ClassroomToolkit.App/Paint/PaintSettingsDialog.SectionState.cs
