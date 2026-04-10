规则ID=R1,R2,R4,R6,R8
影响模块=PaintSettingsDialog(预设托管),PresetSchemePolicy,PresetSchemeInitializationPolicy,PresentationControlOptions,AppSettings
当前落点=src/ClassroomToolkit.App/Paint + src/ClassroomToolkit.Services/Presentation + tests/ClassroomToolkit.Tests
目标归宿=放映控制自动回退参数（失败阈值/探活窗口）可配置、可持久化、可被预设托管且可验证
迁移批次=20260407-自动连续执行-第4批
风险等级=中
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
- powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build: 0 error, 0 warning
- full test: Passed 3193/3193
- contract/invariant: Passed 25/25
- hotspot: PASS（PaintSettingsDialog.xaml.cs=1872 <= budget 1880）
- 新增/更新测试覆盖：
  - PresentationControlServiceTests（自定义降级阈值行为）
  - AppSettingsServiceTests（阈值/探活窗口持久化与归一化）
  - PresetSchemePolicyTests（预设参数匹配与推断）
  - PresetSchemeInitializationPolicyTests（预设初始化写入参数）
回滚动作=
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresentationChoices.cs
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Paint/PresetSchemePolicy.cs
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Paint/PresetSchemeInitializationPolicy.cs
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Settings/AppSettings.cs src/ClassroomToolkit.App/Settings/AppSettingsService.cs
- git restore --source=HEAD~1 src/ClassroomToolkit.Services/Presentation/PresentationControlOptions.cs src/ClassroomToolkit.Services/Presentation/PresentationControlService.cs src/ClassroomToolkit.Services/Presentation/PresentationGateway.cs
- git restore --source=HEAD~1 tests/ClassroomToolkit.Tests/PresetSchemePolicyTests.cs tests/ClassroomToolkit.Tests/PresetSchemeInitializationPolicyTests.cs tests/ClassroomToolkit.Tests/PresentationControlServiceTests.cs tests/ClassroomToolkit.Tests/AppSettingsServiceTests.cs

# Backfill 2026-04-03
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
