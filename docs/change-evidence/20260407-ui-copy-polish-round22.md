# 20260407 UI Copy Polish Round22

## 依据
- 继续压缩启动兼容页和自动修复策略里的动作说明。
- 目标是把长句统一成更短、更直接的表述，同时保留含义。

## 变更
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityAutoRemediationPolicy.cs`
  - 将“自动创建设置目录”和 Office/WPS 兼容动作说明进一步收短。
- `tests/ClassroomToolkit.Tests/StartupCompatibilityAutoRemediationPolicyTests.cs`
  - 同步更新自动修复策略契约。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests|FullyQualifiedName~StartupCompatibilityAutoRemediationPolicyTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若“已创建设置目录”或“已切到兼容优先（PostMessage）”不够明确，回退到上一版表述。
