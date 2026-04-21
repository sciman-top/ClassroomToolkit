# 2026-04-21 Release One-Click Hardening

## Scope
- 边界：补齐项目内缺失的发布入口脚本与发布工作流，维持免费个人项目策略（不引入付费签名）。
- 当前落点：
  - `src/*/packages.lock.json`（新增 `win-x64` 还原目标段，避免后续 RID 发布时重复漂移）
  - `scripts/release/preflight-check.ps1`
  - `scripts/release/prepare-distribution.ps1`
  - `scripts/release/release-config.json`
  - `.github/workflows/release-package.yml`
  - `docs/runbooks/release-checklist.md`
  - `docs/runbooks/no-onsite-compatibility-baseline.md`
- 目标归宿：标准版（FDD）+ 离线版（SCD）可通过脚本一键产出，并固化到 CI 发布流水线。

## Commands
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Release -SkipTests -SkipCompatibilityReport`
  - exit_code=0
  - key_output=`[release-preflight] ALL PASS`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/prepare-distribution.ps1 -Version 2026.04.21-smoke -PackageMode offline -Configuration Release -SkipZip -AllowOverwriteVersion`
  - exit_code=0
  - key_output=`[release-package] manifest: artifacts/release/2026.04.21-smoke/release-manifest.json`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code=0
  - key_output=`已成功生成。0 个警告 0 个错误`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code=0
  - key_output=`已通过! - 失败: 0，通过: 3383，已跳过: 0，总计: 3383`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code=0
  - key_output=`已通过! - 失败: 0，通过: 28，已跳过: 0，总计: 28`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code=0
  - key_output=`[hotspot] PASS - all .cs files within line budget (max=1200)`

## Hotspot Review
- `prepare-distribution.ps1`：
  - 发布参数固定为低误报默认组合：`PublishSingleFile=false`、`PublishTrimmed=false`、SCD 可选 `PublishReadyToRun=true`。
  - 自动产出 `启动.bat`、`bootstrap-runtime.ps1`、`SHA256SUMS.txt`、`release-manifest.json`。
  - 标准版校验 `runtimeconfig/pdfium/e_sqlite3`，离线版校验 `hostfxr/coreclr/vcruntime/e_sqlite3`。
- `release-package.yml`：
  - 支持 `v*` tag 与手动触发。
  - 发布前可执行 `preflight-check.ps1`。
  - 自动上传产物并在 tag 发布时附加 zip + manifest。
- Runbook 对齐：
  - 发布入口与参数从“口径描述”改为“可执行命令”。
  - 增补免费项目低误报策略说明。

## Rollback
1. 删除新增文件：
   - `.github/workflows/release-package.yml`
   - `scripts/release/preflight-check.ps1`
   - `scripts/release/prepare-distribution.ps1`
   - `scripts/release/release-config.json`
   - `docs/change-evidence/20260421-release-oneclick-hardening.md`
2. 回退文档：
   - `docs/runbooks/release-checklist.md`
   - `docs/runbooks/no-onsite-compatibility-baseline.md`
3. 回退后重跑门禁：`build -> test -> contract/invariant -> hotspot`。
