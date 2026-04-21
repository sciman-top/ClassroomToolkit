# 20260421 git-acl-guard hardening

## 变更目标
- 边界：`scripts/git-acl-guard.ps1` + `docs/runbooks/git-metadata-permission-guard.md`
- 当前落点：仓库级 ACL 检测/修复脚本仅做基础 ACL 扫描，跨仓扫描对 `.git` 指针与边界控制不足。
- 目标归宿：支持 `D:\CODE` 多仓库稳定体检，避免误修复到扫描根目录外的 `gitdir`，并提供可复用 runbook。

## 规则与风险
- rule_id: R1/R2/R4/R8
- risk_level: medium（涉及 ACL 修复脚本行为与跨目录扫描范围）

## 主要变更
1. `scripts/git-acl-guard.ps1`
- 新增参数：`-ProbeGitStatus`、`-AllowGitDirOutsideScanRoot`。
- 修复参数互斥校验不可达问题（移到主流程前执行）。
- 增强目标发现：
  - 支持 `.git` 目录与 `.git` 指针文件。
  - 支持输入仓库根目录路径自动解析 `.git`。
  - 扫描时遇到仓库根 `.git` 后停止向下遍历，避免全树深挖超时。
- 新增边界保护：
  - `-ScanAllGitUnder` 默认仅处理扫描根目录内 `gitdir`。
  - 对根目录外 `gitdir` 输出告警并跳过，避免误修复外部 ACL。
- 增强报告字段：`RepositoryRoot`、`SourceKind`、`RequestedFixMode`、`RepairStrategy`、`GitStatusProbe`。

2. `docs/runbooks/git-metadata-permission-guard.md`
- 新增多仓库防复发 runbook（基线、日常、自动化、禁忌、退出码语义）。
- 补充扫描边界说明与显式开关策略。

## 执行命令与关键输出
1. 脚本级回归
- `powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 -ProbeLockFile -ProbeGitStatus`
  - key_output: `ACL clean` + `index.lock probe passed` + `git status probe passed`

2. 仓库扫描回归
- `powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 -ScanAllGitUnder D:\CODE\ClassroomToolkit -ProbeLockFile -ProbeGitStatus -JsonReport docs/change-evidence/20260421-git-acl-guard-smoke.json`
  - key_output: PASS，JSON 报告成功写入

3. 全量扫描（默认边界保护）
- `powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 -ScanAllGitUnder D:\CODE -ProbeLockFile -ProbeGitStatus -JsonReport docs/change-evidence/20260421-git-acl-scan-dcode.json -Quiet`
  - key_output:
    - Skip outside-root warning（`C:\Users\sciman\.codex\memories\v2ray-agent-fix.git`）
    - `D:\CODE` 内已识别仓库全部 PASS

4. 全量扫描（显式允许越界 gitdir）
- `powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 -ScanAllGitUnder D:\CODE -ProbeLockFile -ProbeGitStatus -AllowGitDirOutsideScanRoot -Quiet`
  - key_output: 外部 `gitdir` 检测到 DENY，退出码 `3`

## 硬门禁（固定顺序）
1. `dotnet build ClassroomToolkit.sln -c Debug`
  - PASS（0 warning, 0 error）
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - PASS（3385 passed）
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - PASS（28 passed）
4. `powershell -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - PASS

## hotspot 人工复核
- 复核对象：`scripts/git-acl-guard.ps1`、`docs/runbooks/git-metadata-permission-guard.md`
- 结论：
  - 未改变现有 `-Fix/-LightFix/-AutoFix` 语义顺序；
  - 新增边界保护默认更安全；
  - 回滚路径明确且无业务代码行为回归风险。

## 回滚动作
1. 脚本回滚：
```powershell
git checkout -- scripts/git-acl-guard.ps1
```
2. runbook 回滚：
```powershell
git checkout -- docs/runbooks/git-metadata-permission-guard.md
```
3. 证据文件回滚：
```powershell
git checkout -- docs/change-evidence/20260421-git-acl-guard-hardening.md docs/change-evidence/20260421-git-acl-guard-smoke.json docs/change-evidence/20260421-git-acl-scan-dcode.json
```
