# 2026-04-20 Storage Observability Minimal Diagnostics

## Scope
- Repo: `ClassroomToolkit`
- Goal: 为既有 best-effort 存储写入清理路径补最小诊断信息，保留原有容错语义与行为。

## Rule Mapping
- Global: `R2 R5 R6 R8`
- Project: `C.2 fixed order gates`, `C.3 fail policy`, `C.4 evidence/rollback`

## Changes
- `src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  - temp 清理失败 catch 中新增 `Debug.WriteLine`。
  - load 失败（返回空字典）路径新增 `Debug.WriteLine`。
- `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
  - temp 清理失败 catch 中新增 `Debug.WriteLine`。
  - `TryLoad` 在读取失败/检测到 `\0` 时新增 `Debug.WriteLine`。
- `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
  - temp 清理失败 catch 中新增 `Debug.WriteLine`。
  - self-heal 保存失败 catch 中新增 `Debug.WriteLine`。
  - 保留契约注释字面量：`Best-effort cleanup for temp workbook files.`

## Verification
1. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - exit_code: `0`
   - key_output: `[quality] ALL PASS`
2. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-compatibility-preflight.ps1 -SkipBuild`
   - exit_code: `0`
   - key_output: `[compat-preflight] ALL PASS`

## Risk and Rollback
- Risk level: `low`（仅新增诊断日志）
- Rollback:
  1. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Settings/JsonSettingsDocumentStoreAdapter.cs`
  2. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
  3. `git restore --source=HEAD~1 src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
