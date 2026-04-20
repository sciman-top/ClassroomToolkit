# 2026-04-20 Gate Serialization & PowerShell Invocation Hardening

## Scope
- Repo: `ClassroomToolkit`
- Goal: 降低门禁执行时偶发文件锁重试噪声，统一 preflight 热点脚本调用方式，提升自动执行稳定性。

## Rule Mapping
- Global: `R2 R6 R8`
- Project: `C.2 fixed order gates`, `C.3 fail policy`, `C.4 evidence/rollback`

## Changes
- `scripts/quality/run-local-quality-gates.ps1`
  - `dotnet build` 增加 `-m:1`
  - `dotnet test(full)` 增加 `-m:1`
  - `dotnet test(contract)` 增加 `-m:1`
- `scripts/validation/run-compatibility-preflight.ps1`
  - `dotnet build/test` 增加 `-m:1`
  - hotspot 调用改为 `powershell -NoProfile -ExecutionPolicy Bypass -File ...`

## Gate Evidence
1. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - exit_code: `0`
   - key_output: `[quality] ALL PASS`
2. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-compatibility-preflight.ps1 -SkipBuild`
   - exit_code: `0`
   - key_output: `[compat-preflight] ALL PASS`

## Risk and Rollback
- Risk level: `low`
- Rollback:
  1. `git restore --source=HEAD~1 scripts/quality/run-local-quality-gates.ps1`
  2. `git restore --source=HEAD~1 scripts/validation/run-compatibility-preflight.ps1`
