# ClassroomToolkit 全量治理 v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不破坏现有业务契约与数据兼容的前提下，系统性完成“错误清扫 + 性能优化 + 去冗余 + 可维护性重构 + 长期治理能力建设”。

**Architecture:** 采用“先证据、后改动；先正确性、后性能；先收敛复杂度、后扩展能力”的五阶段推进。所有改动严格遵循归宿边界（Domain/Application/Services/Interop/Infra/App），并通过固定门禁 `build -> test -> contract/invariant -> hotspot` 形成可回滚闭环。

**Tech Stack:** .NET 10, WPF, xUnit, FluentAssertions, PowerShell, GitHub Actions, Coverlet。

---

## Preconditions And Scope

- I'm using the writing-plans skill to create the implementation plan.
- 计划类型：只规划，不在本文件执行阶段直接编码。
- source of truth：
  - `docs/validation/2026-03-18-full-code-audit-playbook.md`
  - `docs/superpowers/plans/2026-03-18-full-code-audit-implementation-plan.md`
  - `AGENTS.md`（全局与项目级）
- 当前规模快照（2026-04-10）：
  - `src` 文件数约 `1017`
  - `tests` 文件数约 `840`
  - `src/ClassroomToolkit.App/Paint` 下 `.cs` 约 `403`
  - `*Policy*.cs` 约 `451`，`*PolicyTests.cs` 约 `446`

## Out Of Scope

- 不做功能需求变更（除非为修复缺陷必须）。
- 不做 UI 视觉重设计。
- 不做跨技术栈迁移（如 WPF -> 其他框架）。

## Success Criteria (Exit)

- 正确性：阻断级缺陷（崩溃/卡死/数据破坏）清零。
- 性能：核心场景达成目标阈值（见 Phase 3）。
- 复杂度：超大热点文件数量下降；重复策略族显著收敛。
- 可维护性：模块边界更清晰，新增规则可被测试与文档约束。
- 治理：证据、回滚、质量门禁与指标联动可持续运行。

## Workstream Overview

### Phase 0: Baseline Evidence & Risk Inventory

**Files:**
- Create: `docs/validation/evidence/2026-04-10-full-governance-v2/00-baseline-and-risk-inventory.md`

- [ ] 建立风险台账（模块、风险类型、当前测试覆盖、缺口、优先级、负责人）。
- [ ] 记录热点清单（优先 >600 行文件 + 高频改动文件 + 历史故障相关文件）。
- [ ] 固化本轮目标阈值（性能、复杂度、稳定性）并写入证据。
- [ ] 明确每项止血补丁的“回收时点 + 最终归宿”。

**Verification:**
- [ ] 文档存在且字段完整。
- [ ] 风险项可追溯到具体文件和测试。

### Phase 1: Correctness & Robustness Sweep (Highest Priority)

**Files likely touched (execution phase):**
- `src/ClassroomToolkit.App/Paint/*`
- `src/ClassroomToolkit.Interop/*`
- `src/ClassroomToolkit.Infra/Storage/*`
- `tests/ClassroomToolkit.Tests/*`
- Create: `docs/validation/evidence/2026-04-10-full-governance-v2/01-correctness-and-robustness.md`

- [ ] 先跑全链路门禁并留档：
  - `dotnet build ClassroomToolkit.sln -c Debug`
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - 契约过滤测试（Architecture/Interop/CrossPage lifecycle）
  - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- [ ] 建立“缺陷 -> 复现测试 -> 修复 -> 回归”闭环模板并强制执行。
- [ ] 优先清扫：异常冒泡、线程阻塞、资源泄漏（COM/Hook/事件）、失败路径清理不完整。
- [ ] 对 Interop/WPS 失败统一执行可降级策略，不允许 UI 崩溃。

**Verification:**
- [ ] 阻断级缺陷全部转绿。
- [ ] 新增缺陷必须对应至少 1 个自动化防回归测试。

### Phase 2: De-duplication & Anti-Overengineering

**Files:**
- Create: `docs/validation/evidence/2026-04-10-full-governance-v2/02-dedup-and-simplification.md`
- Modify (execution phase): `src/ClassroomToolkit.App/Paint/*Policy*.cs`, `*Defaults*.cs`, `*Coordinator*.cs`

- [ ] 识别“同构策略族”并分组（输入相同、分支相同、输出相同）。
- [ ] 合并重复策略，减少“一策略一文件但无独立价值”的拆分。
- [ ] 删除无实际价值的抽象层（单实现接口、仅转发包装器、无边界价值 helper）。
- [ ] 保留必要扩展点，禁止为了未来可能需求做预抽象（YAGNI）。

**Quantitative Targets:**
- [ ] `*Policy*.cs` 数量下降（目标：首轮净减少 8%-15%）。
- [ ] 重复逻辑块（同源复制）显著下降并有证据。

**Verification:**
- [ ] 功能行为不变（测试与手工关键路径验证）。
- [ ] 架构契约测试继续通过。

### Phase 3: Performance & Responsiveness

**Files:**
- Create: `docs/validation/evidence/2026-04-10-full-governance-v2/03-performance-and-latency.md`
- Modify (execution phase): 绘制链路、跨页切换链路、导出链路、图片/PDF加载链路相关文件

- [ ] 建立统一性能基线采集脚本（启动、切页、绘制、导出、内存、GC）。
- [ ] 优先优化 UI 主线程热点与高频分配点。
- [ ] 收敛高频对象分配和重复计算（缓存策略、批处理策略、短生命周期对象管理）。
- [ ] 对卡顿场景加入阈值告警与回归门禁（性能回归测试）。

**Performance Targets (first round):**
- [ ] 启动耗时 P95：下降 15%+。
- [ ] 关键输入到可见绘制延迟 P95：下降 20%+。
- [ ] 大图/PDF切换首帧时间 P95：下降 20%+。
- [ ] 长时运行内存峰值：下降 10%+，无持续增长趋势。

**Verification:**
- [ ] 指标前后对比图与原始日志入档。
- [ ] 质量门禁 + 性能门禁全部通过。

### Phase 4: Maintainability Refactor & Boundary Hardening

**Files:**
- Create: `docs/validation/evidence/2026-04-10-full-governance-v2/04-maintainability-and-boundary.md`
- Modify (execution phase): 大文件热点、模块边界违规点、依赖方向违规点

- [ ] 拆分超大文件（优先 >600 行且高改动频次）。
- [ ] 将业务规则从 UI/Interop 桥接层下沉到 Domain/Application 合法归宿。
- [ ] 补充模块边界契约测试，防止回归到“热点堆叠”。
- [ ] 对公共模式建立清晰命名与目录约束，降低后续认知成本。

**Quantitative Targets:**
- [ ] Top 20 超大文件总行数下降 20%+（首轮目标）。
- [ ] 热点文件变更集中度下降（避免单点超高耦合）。

**Verification:**
- [ ] ArchitectureDependencyTests 通过。
- [ ] 热点预算脚本通过（或按 `gate_na` 规范留证）。

### Phase 5: Long-term Governance Add-ons (Beyond Current 4 Goals)

**Files:**
- Create: `docs/validation/evidence/2026-04-10-full-governance-v2/05-observability-security-release-dataevolution.md`

- [ ] 可观测性：统一结构化日志字段、关键链路 tracing、异常分级告警。
- [ ] 安全与供应链：依赖漏洞扫描、发布产物校验、密钥与配置安全检查。
- [ ] 数据演进：`students.xlsx`/`settings.ini` 变更迁移脚本与回滚演练模板。
- [ ] 发布工程化：灰度开关、回滚演练、性能回归门禁纳入 CI。
- [ ] 开发体验：快速门禁（quick gate）与全门禁职责清晰化。

**Verification:**
- [ ] 新增治理项有可执行 runbook 与证据样例。
- [ ] 至少 1 次演练记录（回滚或故障演练）通过。

## Execution Rhythm

### Suggested Iteration Cadence

- 周节奏：`1 周 1 主题`（正确性 -> 去冗余 -> 性能 -> 可维护性 -> 治理补齐）。
- 每周固定 checkpoint：
  - [ ] 任务完成率
  - [ ] 质量门禁状态
  - [ ] 新增风险与回滚准备度

### Hard Gates (Do Not Bypass)

按固定顺序执行，除非符合 `platform_na/gate_na` 且有证据：
1. `build`
2. `test`
3. `contract/invariant`
4. `hotspot`

## Evidence Pack Template

每个阶段证据最少包含：
- [ ] 依据（规则/需求/问题单）
- [ ] 命令（含参数）
- [ ] 结果（关键输出摘要）
- [ ] 结论（是否通过/风险级别）
- [ ] 回滚动作（可执行）
- [ ] N/A 字段（如适用：`reason/alternative_verification/evidence_link/expires_at`）

## Risks And Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| 过度重构导致行为漂移 | High | 小步提交，先补测试再动代码，关键路径人工回归 |
| 性能优化引入稳定性回退 | High | 双门禁（功能+性能）同时通过才可合并 |
| 去冗余误删真实差异逻辑 | Medium | 先分组比对输入输出契约，再合并 |
| 长周期治理中断 | Medium | 周度 checkpoint + 证据化推进，避免“大爆炸”改造 |

## Rollback Strategy

- 任一阻断级问题（崩溃、卡死、输入失效、数据写坏）立即停止推进当前批次。
- 回滚依据：`docs/runbooks/migration-rollback-playbook.md`。
- 回滚后必须重跑：`build -> test -> contract/invariant -> hotspot`。

## Open Questions (To Confirm Before Execution)

- [ ] 性能阈值是否采用“统一全局阈值”，还是按场景分层阈值（课堂演示/日常备课）？
- [ ] 去冗余目标是否优先 `Paint` 子域，还是并行覆盖 `Interop/Infra`？
- [ ] 首轮周期是否按 5 周推进，还是压缩到 3 周（风险更高）？

