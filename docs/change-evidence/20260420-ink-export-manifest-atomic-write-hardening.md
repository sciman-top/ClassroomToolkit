# 2026-04-20 Ink Export Manifest Atomic Write Hardening

## Scope
- Repo: `ClassroomToolkit`
- Goal: 将导出 manifest 写入从直接覆盖改为临时文件 + 原子替换，降低异常中断导致的部分写入风险。

## Rule Mapping
- Global: `R2 R5 R6 R8`
- Project: `C.2 fixed order gates`, `C.3 fail policy`, `C.4 evidence/rollback`

## Changes
- `src/ClassroomToolkit.App/Ink/InkExportManifestUtilities.cs`
  - 新增 `AtomicFileReplaceUtility` 引用。
  - `SaveExportManifest` 改为：
    1. 先写 `${manifestPath}.{guid}.tmp`
    2. 目标存在时走 `AtomicFileReplaceUtility.ReplaceOrOverwrite`
    3. 目标不存在时 `File.Move`
    4. `finally` 中 best-effort 清理 temp 文件并记录诊断日志
  - 保持原语义：异常仍被捕获，不阻断导出流程。

## Verification
1. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - exit_code: `0`
   - key_output: `[quality] ALL PASS`
2. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-compatibility-preflight.ps1 -SkipBuild`
   - exit_code: `0`
   - key_output: `[compat-preflight] ALL PASS`

## Risk and Rollback
- Risk level: `low`
- Rollback:
  1. `git restore --source=HEAD~1 src/ClassroomToolkit.App/Ink/InkExportManifestUtilities.cs`
