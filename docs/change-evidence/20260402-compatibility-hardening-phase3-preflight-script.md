# 变更证据：20260402 兼容性增强 Phase3（兼容预检脚本）

当前落点=新增兼容性预检脚本，固化发布前兼容门禁链路（build/test/contract/hotspot）
目标归宿=降低人工执行门禁遗漏风险，支持矩阵批次快速复验
风险等级=低

执行命令=powershell -File scripts/validation/run-compatibility-preflight.ps1 -Configuration Debug
验证证据=script reported build PASS; full test 3085/3085 pass; contract 24/24 pass; hotspot PASS; final line=[compat-preflight] ALL PASS

变更文件=
- scripts/validation/run-compatibility-preflight.ps1

回滚动作=git restore --source=HEAD~1 -- scripts/validation/run-compatibility-preflight.ps1 docs/change-evidence/20260402-compatibility-hardening-phase3-preflight-script.md
