# 20260407 UI Copy Polish Round26

## 依据
- 继续压缩启动兼容快速修复里的权限重启、标准包重装和架构建议。
- 目标是在保留可执行性的前提下，进一步减少句长。

## 变更
- `src/ClassroomToolkit.App/Startup/StartupOrchestrator.cs`
  - 将快速修复第 2、3 条和架构/未知架构提示进一步收短。
- `tests/ClassroomToolkit.Tests/App/StartupCompatibilityWarningCopyContractTests.cs`
  - 同步更新快速修复契约。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests|FullyQualifiedName~StartupCompatibilityAutoRemediationPolicyTests|FullyQualifiedName~StartupCompatibilityWarningCopyContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若快速修复步骤过于简略，回退到上一版表述。
