# 20260407 UI Copy Polish Round24

## 依据
- 继续压缩启动兼容输出里仍然略长的结果句和自动修复防抖说明。
- 目标是在不改变含义的前提下，减少句尾冗余。

## 变更
- `src/ClassroomToolkit.App/Startup/StartupOrchestrator.cs`
  - 将启动结果句收短为“程序将继续启动。请尽快修复。”。
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityAutoRemediationPolicy.cs`
  - 将防抖说明收短为“已启用“降级后固定兼容模式”。”。
- `tests/ClassroomToolkit.Tests/App/StartupCompatibilityWarningCopyContractTests.cs`
  - 同步更新启动兼容结果句契约。

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
- 若结果句或防抖说明过短影响可读性，回退到上一版表述。
