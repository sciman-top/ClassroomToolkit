# 20260421 外部 gitdir ACL 修复留痕

## 目标
- issue_id: `git-acl-outside-gitdir-v2ray-agent`
- boundary: `C:\Users\sciman\.codex\memories\v2ray-agent-fix.git`（来自 `D:\CODE\vps-ssh-launcher\sciman-v2ray-agent` 的 `.git` 指针）
- 目标归宿：恢复该仓库 Git 元数据可写能力，避免 `index.lock/permission denied` 类问题反复出现。

## 尝试与结论
- attempt_count: `3`
- clarification_mode: `direct_fix`
- attempt_1（`-LightFix`）：失败（DENY 876 未变化）
- attempt_2（`-AutoFix`）：失败（DENY 876 未变化）
- attempt_3（定向继承链修复）：成功（全量扫描 PASS）

## 根因
- 目标 `gitdir` 的 DENY ACE 主要为 inherited（来自父目录 ACL 继承链）。
- 脚本对目标目录重置后重新开启继承，会把父级 DENY 再次带回，导致“修了又回”。

## 实施动作（成功方案）
1. 备份 ACL  
   - `icacls <target> /save <backup> /T /C`
2. 重置为继承基线  
   - `icacls <target> /reset /T /C`
3. 去除继承项（仅目标树）  
   - `icacls <target> /inheritance:r /T /C`
4. 重建最小必要权限  
   - 当前用户：`(OI)(CI)F`
   - `NT AUTHORITY\SYSTEM:(OI)(CI)F`
   - `BUILTIN\Administrators:(OI)(CI)F`

## 关键命令与结果
1. 修复后全量复扫（含外部 gitdir）：
```powershell
powershell -ExecutionPolicy Bypass -File scripts/git-acl-guard.ps1 `
  -ScanAllGitUnder D:\CODE `
  -AllowGitDirOutsideScanRoot `
  -ProbeLockFile `
  -ProbeGitStatus `
  -JsonReport docs/change-evidence/20260421-git-acl-postfix-scan.json `
  -Quiet
```
- key_output: 5/5 目标全部 `ACL clean + index.lock probe passed + git status probe passed`
- exit_code: `0`

2. 目标仓库实操验证：
```powershell
git -C D:\CODE\vps-ssh-launcher\sciman-v2ray-agent status --short --branch
```
- key_output: 正常返回分支状态，无权限错误。

## 证据文件
- `docs/change-evidence/20260421-git-acl-dcode-lightfix.json`
- `docs/change-evidence/20260421-git-acl-dcode-autofix.json`
- `docs/change-evidence/20260421-git-acl-postfix-scan.json`

## 回滚
- 优先使用 ACL 备份回滚：
```powershell
icacls . /restore "D:\CODE\ClassroomToolkit\acl-backup-git-manual-20260421-204904.txt"
```
- 备用备份：
  - `D:\CODE\ClassroomToolkit\acl-backup-git-20260421-204739-1.txt`
  - `D:\CODE\ClassroomToolkit\acl-backup-git-20260421-204802-1.txt`
