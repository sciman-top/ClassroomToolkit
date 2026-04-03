规则ID=R1,R2,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml
当前落点=学生大照片全屏窗口顶部姓名字样
目标归宿=透明底姓名标题样式，只调整可读性与视觉表现，不改变数据绑定和布局归宿
迁移批次=2026-04-04-photos-overlay-name-clarity
风险等级=低
执行命令=codex status; codex --version; codex --help; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build通过(0 warning/0 error); 全量测试通过(3171/3171); contract/invariant通过(25/25); hotspot=PASS; 恢复合适字号52，去除黑色背景小框、彩色重影和姓名自身阴影，改为透明底细描边字
回滚动作=git checkout HEAD -- src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml docs/change-evidence/20260404-photo-overlay-name-clarity.md

platform_na:
- reason=`codex status` 在当前非交互终端返回 `stdin is not a terminal`
- alternative_verification=`codex --version` 返回 `codex-cli 0.118.0`，`codex --help` 返回命令帮助；活动规则来源为仓库根 `E:\CODE\ClassroomToolkit\AGENTS.md`；本轮并行测试曾触发 WPF 临时输出文件锁，随后串行复跑全量测试通过
- evidence_link=`docs/change-evidence/20260404-photo-overlay-name-clarity.md`
- expires_at=`2026-04-11`
