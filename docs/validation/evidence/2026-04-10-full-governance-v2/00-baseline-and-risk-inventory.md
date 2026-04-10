# 2026-04-10 Full Governance v2 - Phase 0 Baseline And Risk Inventory

## 1) Scope & Objective

- scope: 全仓（`src/` + `tests/`）治理前基线与风险分层建档
- objective: 为后续 Phase 1-5 提供可执行优先级、量化阈值与回滚入口
- mode: planning only（本阶段不做业务代码改动）

## 2) Evidence: Command Trace

### 2.1 Tooling precheck

- cmd: `Get-Command dotnet | Select-Object -ExpandProperty Source`
  - key_output: `C:\Program Files\dotnet\dotnet.exe`
- cmd: `Get-Command powershell | Select-Object -ExpandProperty Source`
  - key_output: `C:\\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`
- cmd: `Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
  - key_output: `True`

### 2.2 Repository size snapshot

- cmd: `(rg --files src | Measure-Object).Count`
  - key_output: `1017`
- cmd: `(rg --files tests | Measure-Object).Count`
  - key_output: `840`
- cmd: `(Get-ChildItem -Path src -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | Measure-Object).Count`
  - key_output: `979`
- cmd: `(Get-ChildItem -Path tests -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | Measure-Object).Count`
  - key_output: `818`

### 2.3 Complexity hotspot snapshot

- cmd: `(Get-ChildItem -Path src\\ClassroomToolkit.App\\Paint -Filter *.cs | Measure-Object).Count`
  - key_output: `403`
- cmd: `(Get-ChildItem -Path src -Recurse -Filter *Policy*.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | Measure-Object).Count`
  - key_output: `451`
- cmd: `(Get-ChildItem -Path tests\\ClassroomToolkit.Tests -Recurse -Filter *PolicyTests.cs | Measure-Object).Count`
  - key_output: `446`
- cmd: `(Get-ChildItem -Path src -Recurse -Filter *Defaults*.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | Measure-Object).Count`
  - key_output: `65`

### 2.4 Top large source files (line-based)

1. `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.cs` (`1217`)
2. `src/ClassroomToolkit.App/Ink/InkExportService.cs` (`1111`)
3. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs` (`1103`)
4. `src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.cs` (`1060`)
5. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.cs` (`1038`)
6. `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs` (`976`)
7. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs` (`918`)
8. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs` (`913`)
9. `src/ClassroomToolkit.App/MainWindow.xaml.cs` (`895`)
10. `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.cs` (`891`)

### 2.5 Working tree snapshot

- cmd: `git status --short`
- key_output:
  - `M docs/PLANS.md`
  - `?? docs/superpowers/plans/2026-04-10-full-governance-v2-implementation-plan.md`

### 2.6 Codex minimum diagnostics (project-required)

- cmd: `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
  - classification: `platform_na`
  - reason: 非交互终端限制，`codex status` 无法执行
  - alternative_verification: 执行 `codex --version` 与 `codex --help` 作为替代证据
  - evidence_link: 本文档
  - expires_at: `2026-04-11`
- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.118.0`
- cmd: `codex --help`
  - exit_code: `0`
  - key_output: `Codex CLI` 帮助输出正常

## 3) Risk Inventory (Initial)

| Priority | Module/Area | Main Risk | Evidence | Phase |
|---|---|---|---|---|
| P0 | `App/Paint` cross-page/ink pipeline | UI卡顿、输入延迟、状态重入、大文件高耦合 | `Paint` 403 files + 多个 900+ 行热点 | 1,2,3,4 |
| P0 | `Interop` hook/com lifecycle | 资源泄漏、异常冒泡、WPS不可用时降级不稳 | 历史审查手册重点 + 契约测试要求 | 1 |
| P0 | `Infra/Storage` | 数据一致性与兼容风险（`students.xlsx`/`settings.ini`） | 项目级约束明确为强兼容保护 | 1,5 |
| P1 | `App/MainWindow` + `Windowing` | 事件订阅复杂、焦点与置顶策略冲突 | 主窗口热点文件体量较大 | 1,4 |
| P1 | `Policy/Defaults` family | 过度细分、重复策略、认知负担高 | `451` policy files, `65` defaults | 2,4 |
| P2 | `Services/Compatibility` startup path | 启动兼容探测路径复杂、失败恢复成本高 | `StartupCompatibilityProbe.cs` 807 行 | 3,4 |

## 4) Target Baseline (To Be Enforced)

### 4.1 Correctness/Stability

- 阻断级缺陷（崩溃/卡死/数据写坏）目标：`0`
- 每个缺陷闭环：`复现测试 -> 修复 -> 回归测试 -> 证据`

### 4.2 Performance

- 启动时间 P95：相对基线下降 `>=15%`
- 输入到可见绘制延迟 P95：下降 `>=20%`
- 大图/PDF 切换首帧 P95：下降 `>=20%`
- 长时运行内存峰值：下降 `>=10%` 且无持续增长趋势

### 4.3 Complexity

- `*Policy*.cs` 首轮净减少：`8%-15%`
- Top 20 大文件总行数：下降 `>=20%`（以不破坏行为为前提）

## 5) Batch Plan (Execution Queue)

1. Batch A (P0 correctness):
   - `App/Paint` 输入/跨页/导出链路
   - `Interop` 生命周期与降级
   - `Infra` 持久化一致性
2. Batch B (P1 simplification):
   - `Policy/Defaults` 族收敛
   - `MainWindow/Windowing` 事件编排简化
3. Batch C (P1/P2 performance + maintainability):
   - 绘制路径与分配热点优化
   - 大文件拆分与边界回归约束
4. Batch D (governance add-ons):
   - 可观测性/供应链安全/发布回滚演练

## 6) Gate Contract

固定顺序，不可绕过：
1. build: `dotnet build ClassroomToolkit.sln -c Debug`
2. test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. contract/invariant:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. hotspot:
   - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 7) N/A Policy For This Phase

- `build/test/contract/hotspot` 本阶段未执行，原因：Phase 0 仅做规划与基线采集，不做代码修复。
- 分类：`gate_na`
- reason: 规划阶段，无实现改动，不触发代码门禁。
- alternative_verification: 工具可用性 precheck + 仓库规模/热点/风险量化已完成。
- evidence_link: 本文档。
- expires_at: `2026-04-11`（进入 Batch A 前必须恢复正常门禁执行）。

## 8) Rollback Entry

- 本阶段仅新增/更新文档，无业务代码变更。
- 若需回滚，删除本轮新增文档并还原规划入口文件改动即可：
  - `docs/validation/evidence/2026-04-10-full-governance-v2/00-baseline-and-risk-inventory.md`
  - `docs/PLANS.md`
  - `docs/superpowers/plans/2026-04-10-full-governance-v2-implementation-plan.md`
