# Git 元数据权限防复发 Runbook（Windows / D:\CODE）

## 1. 目标
- 统一治理 `D:\CODE` 下多仓库的 Git 元数据权限问题。
- 先检测、后修复，保留回滚证据，避免反复人工排障。

## 2. 常见症状
- `Unable to create '.git/index.lock': Permission denied`
- `insufficient permission for adding an object to repository database`
- `fatal: could not open '.git/...'`

## 3. 根因模型（高频）
- `.git` 下存在 `DENY` ACL（显式拒绝）或继承异常。
- 目录曾被不同权限上下文混用（管理员/普通用户交替写入）。
- 文件同步/安全软件对 `.git` 元数据瞬时占用或权限收紧。
- 旧锁文件或异常退出导致 Git 元数据状态异常。

## 4. 一次性基线（建议先执行）
1. 只读体检（全仓库）：
```powershell
powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 `
  -ScanAllGitUnder 'D:\CODE' `
  -ProbeLockFile `
  -ProbeGitStatus `
  -JsonReport "docs/change-evidence/$(Get-Date -Format 'yyyyMMdd')-git-acl-scan.json"
```

说明：
- `-ScanAllGitUnder` 默认只处理扫描根目录内的 `gitdir`。
- 若仓库使用 `.git` 指针并把 `gitdir` 放在根目录外，脚本会告警并跳过（防止误改外部 ACL）。
- 仅在你明确需要时，才加 `-AllowGitDirOutsideScanRoot`。

2. 轻修复（优先，尽量不重置 ACL 结构）：
```powershell
powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 `
  -ScanAllGitUnder 'D:\CODE' `
  -LightFix `
  -ProbeLockFile `
  -ProbeGitStatus `
  -JsonReport "docs/change-evidence/$(Get-Date -Format 'yyyyMMdd')-git-acl-lightfix.json"
```

3. 自动升级修复（仅在轻修复无法收敛时）：
```powershell
powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 `
  -ScanAllGitUnder 'D:\CODE' `
  -AutoFix `
  -ProbeLockFile `
  -ProbeGitStatus `
  -JsonReport "docs/change-evidence/$(Get-Date -Format 'yyyyMMdd')-git-acl-autofix.json"
```

## 5. 日常防复发
- 每天开工前运行一次“只读体检”命令。
- 每次批量拉取/切分支后，若出现权限报错先跑 `-LightFix`，再继续开发。
- 所有修复保留 JSON 报告，便于追踪仓库、时间和 SID。

## 6. 建议自动化（任务计划程序）
- 触发：工作日登录后或每天固定时间。
- 动作：执行第 1 节“只读体检”命令。
- 失败通知：任务失败时弹窗或写入统一日志目录。

## 7. 操作禁忌（最关键）
- 不要在同一工作区混用管理员终端与普通终端执行 Git 写操作。
- 尽量避免把活跃仓库放在实时同步冲突高的目录（尤其含大量小文件变更时）。
- 不要手工删除/修改 `.git` 内部结构文件（除非先备份并有明确恢复方案）。

## 8. 失败时回滚
- `git-acl-guard` 修复会先导出 ACL 备份（`icacls /save`）。
- 回滚命令：
```powershell
icacls . /restore "<backup-path>"
```

## 9. 继承链污染的定向修复（高级）
- 场景：`-LightFix/-AutoFix` 均失败，且 `icacls` 显示 DENY 为 inherited（`(I)`）。
- 目标：只修复故障 `gitdir`，不改父目录 ACL。

```powershell
$target = "C:\path\to\broken.git"
$me = "$env:USERDOMAIN\$env:USERNAME"

icacls $target /save "D:\CODE\ClassroomToolkit\acl-backup-manual.txt" /T /C
icacls $target /reset /T /C
icacls $target /inheritance:r /T /C
icacls $target /grant:r "${me}:(OI)(CI)F" /T /C
icacls $target /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F" /T /C
icacls $target /grant "BUILTIN\Administrators:(OI)(CI)F" /T /C
```

执行后用下列命令复核：
```powershell
powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 `
  -ScanAllGitUnder D:\CODE `
  -AllowGitDirOutsideScanRoot `
  -ProbeLockFile `
  -ProbeGitStatus `
  -Quiet
```

## 10. 退出码语义（脚本）
- `0`：通过（检测通过或修复后通过）。
- `3`：检测到问题，且未启用修复开关。
- `8`：启用了修复但最终未收敛。
- `2`：参数或输入路径错误。
