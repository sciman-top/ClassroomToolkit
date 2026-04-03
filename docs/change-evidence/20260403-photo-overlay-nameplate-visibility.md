规则ID=R1,R2,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml; src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs
当前落点=学生大照片全屏窗口顶部姓名徽标
目标归宿=照片展示链路内聚的姓名渲染与定位逻辑，不改学生数据与图片加载语义
迁移批次=2026-04-03-photos-overlay-nameplate
风险等级=低
执行命令=codex status; codex --version; codex --help; Get-Command dotnet; Get-Command powershell; Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj; dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build通过(0 warning/0 error); 全量测试通过(3171/3171); contract/invariant通过(25/25); hotspot=PASS; 顶部姓名改为 NameBadge 整体定位，字号提升到64，楷体优先并增加彩色描边层
回滚动作=git checkout HEAD -- src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs docs/change-evidence/20260403-photo-overlay-nameplate-visibility.md

platform_na:
- reason=`codex status` 在当前非交互终端返回 `stdin is not a terminal`
- alternative_verification=`codex --version` 返回 `codex-cli 0.118.0`，`codex --help` 返回命令帮助；活动规则来源为仓库根 `E:\CODE\ClassroomToolkit\AGENTS.md`
- evidence_link=`docs/change-evidence/20260403-photo-overlay-nameplate-visibility.md`
- expires_at=`2026-04-10`
