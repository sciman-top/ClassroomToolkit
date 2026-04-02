# Governance-Kit 终态收敛路线图（ClassroomToolkit）

日期：2026-04-03  
适用仓库：`E:/CODE/ClassroomToolkit`  
source of truth：`E:/CODE/governance-kit/source/project/ClassroomToolkit/*`

## 1. 目标定义（终态不是一次性状态）

终态定义为“持续收敛状态”，满足以下 4 个条件并连续维持 4 周：

1. 硬门禁通过率稳定：`build -> test -> contract/invariant -> hotspot` 每日主线通过率 >= 95%。
2. Waiver 清零机制有效：`waiver_expired_unrecovered_count = 0`，且无长期豁免（>14天）。
3. 证据闭环完整：`docs/change-evidence/*` 完整字段率 >= 98%。
4. 热点规模持续收敛：热点预算超限条目为 0，且预算季度递减。

## 2. 终态指标表（可直接写入 metrics-template）

| 指标 | 字段 | 目标阈值 | 采集方式 | 失败动作 |
|---|---|---:|---|---|
| 硬门禁通过率 | `gate_pass_rate` | >=95%（7日滚动） | CI + 本地 `run-local-quality-gates.ps1` 结果聚合 | 阻断发布，定位失败段 |
| 回滚率 | `rollback_rate` | <=5%（30日） | change-evidence 回滚动作统计 | 触发根因复盘 |
| 止血补丁逾期率 | `patch_recovery_overdue_rate` | 0 | waiver/recovery_due 扫描 | 升级为高风险阻断 |
| 证据完整率 | `evidence_completeness_rate` | >=98% | `template.md` 字段覆盖检查 | 阻断合并 |
| 活跃 Waiver 数 | `waiver_active_count` | 下降趋势 | `docs/governance/waivers` 扫描 | 超阈值触发治理告警 |
| 过期未回收 Waiver | `waiver_expired_unrecovered_count` | 0 | 到期时间 + 状态扫描 | 高风险阻断 |

## 3. 当前能力与缺口（基于本仓现状）

已具备：

- 已有硬门禁编排：`scripts/quality/run-local-quality-gates.ps1`。
- 已有 hotspot 预算校验：`scripts/quality/check-hotspot-line-budgets.ps1`。
- 已有模板：`docs/change-evidence/template.md`、`docs/governance/metrics-template.md`、`docs/governance/waiver-template.md`。

主要缺口：

1. 缺“终态评分/差距报告（gap report）”统一出口。  
2. 缺“waiver 到期与恢复计划”自动阻断扫描。  
3. 缺“证据字段完整率”自动校验。  
4. 缺“一键收敛命令”（install + doctor + gate + report）。

## 4. 改造清单（按优先级）

### P0（本周，先落地阻断能力）

1. 新增 `scripts/governance/run-doctor-endstate.ps1`
- 作用：汇总状态、输出终态分数与 gap 列表（Markdown + JSON）。
- 输入：仓库现有门禁结果、waiver 文件、change-evidence 文件。
- 输出：`docs/governance/reports/endstate-YYYYMMDD-HHmmss.{md,json}`。

2. 新增 `scripts/governance/check-waiver-health.ps1`
- 作用：扫描 `docs/governance/waivers/*.md`，校验 `owner/expires_at/status/recovery_plan/evidence_link`。
- 阻断条件：存在过期未回收 waiver、缺必填字段。

3. 新增 `scripts/governance/check-evidence-completeness.ps1`
- 作用：按 `docs/change-evidence/template.md` 校验新增证据文件字段覆盖率。
- 阻断条件：完整率 < 98% 或关键字段缺失（规则ID/执行命令/验证证据/回滚动作）。

### P1（两周内，做收敛驱动）

1. 增强 `scripts/quality/run-local-quality-gates.ps1`
- 在 `hotspot` 后串联：`check-waiver-health`、`check-evidence-completeness`。
- 增加 `-EmitGovernanceReport` 开关，自动产出 gap report。

2. 新增 `scripts/governance/run-endstate-loop.ps1`
- 单命令流程：precheck -> hard gates -> governance checks -> doctor report。
- 产物统一落库：`docs/governance/reports/`。

3. 更新 CI 入口
- 在 `.github/workflows/quality-gate.yml` 增加治理检查步骤。
- 失败即阻断，不允许仅警告通过。

### P2（本月内，做目标仓群体化推广）

1. governance-kit `install.ps1` 增加“终态模式”参数（如 `-Profile endstate`）。
2. governance-kit `doctor.ps1` 增加多仓汇总视图（每仓终态分数 + Top3 gaps）。
3. 建立季度预算收敛计划（hotspot budget 自动建议下调）。

## 5. 执行节奏（建议）

1. D1-D2：完成 P0 三个脚本与本仓接入。  
2. D3-D4：CI 阻断联动 + 报告落库。  
3. D5：回灌 `governance-kit/source/project/ClassroomToolkit/*` 并执行：

```powershell
powershell -File E:/CODE/governance-kit/scripts/install.ps1 -Mode safe
powershell -File E:/CODE/governance-kit/scripts/doctor.ps1
```

## 6. 验收命令（本仓）

```powershell
# precheck
Get-Command dotnet
Get-Command powershell
Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj

# hard gates
powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Debug

# governance checks (P0新增后)
powershell -File scripts/governance/check-waiver-health.ps1
powershell -File scripts/governance/check-evidence-completeness.ps1
powershell -File scripts/governance/run-doctor-endstate.ps1
```

## 7. 回滚策略

- 脚本级回滚：新增脚本可直接删除并回退 CI 调用点。
- 规则级回滚：通过 waiver 临时放行必须带 `expires_at` 与 `recovery_plan`。
- 安装级回滚：重新执行 governance-kit safe install 覆盖到上一稳定版。

## 8. 本次任务映射（Global -> Repo）

- R1：先明确落点（本仓）与归宿（governance-kit source of truth）。
- R4/R6：将 waiver/evidence 纳入阻断门禁，维持固定顺序。
- R8/E3：报告与证据产物统一落 `docs/governance/reports` 与 `docs/change-evidence`。
- E4：把终态指标与门禁结果绑定。

## 9. 下一步最小开工包

1. 新建 `scripts/governance/` 目录。  
2. 先实现 `check-waiver-health.ps1`（阻断价值最高）。  
3. 再实现 `check-evidence-completeness.ps1`。  
4. 最后实现 `run-doctor-endstate.ps1` 并接入 `run-local-quality-gates.ps1`。
