# 20260407 UI Copy Polish Round20

## 依据
- 继续压缩启动兼容提示页和启动运行时输出里仍然偏长的修复说明。
- 目标是让快速修复、自动修复和结果句口径一致，且更短。

## 变更
- `src/ClassroomToolkit.App/Startup/StartupOrchestrator.cs`
  - 将快速修复、结果句和兼容建议进一步收短。
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityAutoRemediationPolicy.cs`
  - 将自动修复动作说明进一步收短。
- `tests/ClassroomToolkit.Tests/App/StartupCompatibilityWarningCopyContractTests.cs`
  - 同步更新启动兼容快速修复契约。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若快速修复步骤或兼容提示过于简略，回退到上一版表述。

# Backfill 2026-04-03
规则ID=BACKFILL-LEGACY-EVIDENCE-2026-04-03
影响模块=legacy-governance-evidence
当前落点=D:/OneDrive/CODE/ClassroomToolkit/docs/change-evidence
目标归宿=D:/OneDrive/CODE/repo-governance-hub/source/project/ClassroomToolkit/*
迁移批次=2026-04-03-evidence-backfill
风险等级=Low(documentation backfill only)
执行命令=backfill-evidence-template-fields.ps1
验证证据=template-field-backfill-2026-04-03
回滚动作=git revert evidence backfill commit
