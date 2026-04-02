规则ID=R1,R2,R3,R4,R6,R8
影响模块=RollCall 学生照片展示（ViewModel -> XAML Image Source）
当前落点=src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Navigation.cs
目标归宿=在 ViewModel 层统一执行照片路径切换去旧帧策略（null -> newPath）
迁移批次=2026-04-02-batch-rollcall-photo-refresh
风险等级=Low
执行命令=codex status; codex --version; codex --help; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests.SetCurrentStudentByIndex_ShouldClearPreviousPhotoPath_BeforeApplyingNextPhotoPath"; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=新增失败用例先红后绿；全量测试 3112/3112 通过；contract/invariant 24/24 通过；hotspot PASS
回滚动作=回滚 src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Navigation.cs 中 RefreshCurrentStudentPhotoPath 及其调用；删除新增测试用例并复跑同序门禁

## platform_na
- type: platform_na
- cmd: codex status
- exit_code: 1
- reason: 非交互终端执行，返回 `stdin is not a terminal`
- alternative_verification: 通过 `codex --version` 与 `codex --help` 完成可用性确认；active_rule_path 采用仓库根 AGENTS.md 与 GlobalUser/AGENTS.md 语义承接
- evidence_link: docs/change-evidence/20260402-rollcall-photo-previous-frame-flash.md
- expires_at: 2026-04-30
