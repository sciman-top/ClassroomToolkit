# 2026-04-20 Auto Remediation — Quality & Resilience

## Scope
- Repo: `ClassroomToolkit`
- Goal: 落地高优先级修复（门禁脚本可靠性、CI fail-fast、异常降级边界、日志资源保护、启动探测冗余消减）

## Rule Mapping
- Global: `R1 R2 R3 R4 R6 R8`
- Project: `C.2 fixed order gates`, `C.3 fail policy`, `C.4 evidence/rollback`

## Change Set
- `scripts/validation/run-compatibility-preflight.ps1`
  - 对原生命令新增非零退出码阻断（避免“命令失败但步骤通过”）。
- `scripts/quality/run-local-quality-gates.ps1`（新增）
  - 本地一键门禁：`build -> test(full) -> test(contract) -> hotspot`。
- `scripts/quality/check-hotspot-line-budgets.ps1`（新增）
  - 热点审查脚本，限制 `src/**/*.cs` 行数预算（默认 `1200`）。
  - 已修复字符串插值 bug：`"${relative}:$lineCount"`。
- `azure-pipelines.yml` / `.gitlab-ci.yml`
  - 质量脚本缺失时由“跳过”改为“失败阻断”。
- `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
  - 仅在“格式/损坏类”读取异常时才 fallback 到模板；IO/权限/路径类异常不再静默降级。
- `src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs`
  - 启动探测复用 token 构造结果，消除重复解析。
- `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
  - 日志队列改为有界（`8192`），满载时丢弃并周期输出丢弃计数。
- `src/ClassroomToolkit.App/App.xaml.cs`
  - 默认日志级别：`DEBUG=Debug`，其他构建=`Information`。
  - 启动不再重置历史日志文件。

## Gate Execution Evidence
1. `dotnet build ClassroomToolkit.sln -c Debug -m:1`
   - exit_code: `0`
   - key_output: `0 errors`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
   - exit_code: `0`
   - key_output: `Passed 3329`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit_code: `0`
   - key_output: `Passed 28`
4. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - exit_code: `0`
   - key_output: `[quality] ALL PASS`
5. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-compatibility-preflight.ps1 -SkipBuild`
   - exit_code: `0`
   - key_output: `[compat-preflight] ALL PASS`

## Platform Diagnostic Matrix
- `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.121.0`
- `codex --help`
  - exit_code: `0`
  - key_output: help shown
- `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
  - classification: `platform_na`
  - reason: 非交互 shell 无 TTY。
  - alternative_verification: 使用 `codex --version` + `codex --help` + 本地门禁链路结果作为替代证据。
  - evidence_link: `docs/change-evidence/20260420-auto-remediation-quality-and-resilience.md`
  - expires_at: `2026-05-20`

## Risk and Rollback
- 风险等级：`中`（涉及脚本门禁行为、日志策略与异常降级边界）
- 回滚动作：
  1. `git restore --source=HEAD~1 azure-pipelines.yml .gitlab-ci.yml`
  2. `git restore --source=HEAD~1 scripts/validation/run-compatibility-preflight.ps1`
  3. `git restore --source=HEAD~1 scripts/quality/run-local-quality-gates.ps1 scripts/quality/check-hotspot-line-budgets.ps1`
  4. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
  5. `git restore --source=HEAD~1 src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs`
  6. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
  7. `git restore --source=HEAD~1 src/ClassroomToolkit.App/App.xaml.cs`

