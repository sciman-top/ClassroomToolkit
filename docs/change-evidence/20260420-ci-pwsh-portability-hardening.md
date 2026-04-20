# 2026-04-20 CI Pwsh Portability Hardening

## Scope
- issue_id: `ci-powershell-executable-portability`
- boundary: `.gitlab-ci.yml`, `azure-pipelines.yml`, `scripts/quality/run-local-quality-gates.ps1`, `scripts/validation/run-compatibility-preflight.ps1`
- target: 降低 CI/脚本对 Windows PowerShell (`powershell`) 的硬依赖，优先使用 `pwsh`

## Risk
- level: `low`
- compatibility: 不改业务逻辑、外部接口、数据与配置语义

## Changes
1. `.gitlab-ci.yml` 统一以 `pwsh` 调用质量门禁脚本。
2. `azure-pipelines.yml` 统一以 `pwsh` 调用质量门禁脚本。
3. `scripts/quality/run-local-quality-gates.ps1` 新增 `Resolve-PowerShellExecutable`，优先 `pwsh`，回退 `powershell`。
4. `scripts/validation/run-compatibility-preflight.ps1` 的 hotspot 步骤采用同样解析策略。

## Verification
- `dotnet build ClassroomToolkit.sln -c Debug` -> pass
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> pass (`3337 passed`)
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` -> pass (`28 passed`)
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` -> pass

## Rollback
1. 回退上述 4 个文件在本次提交中的改动。
2. 重跑四段门禁命令验证回退后状态一致。

