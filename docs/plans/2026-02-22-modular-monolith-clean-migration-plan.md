# ClassroomToolkit 架构升级计划（Modular Monolith + Clean）

## 1. 结论与决策
- 结论：可以改，而且建议改为“渐进式迁移”，不做一次性重写。
- 目标架构：`Modular Monolith + Clean 分层`。
- 分层边界：`App/UI`、`Application`、`Domain`、`Infrastructure`、`Interop.Adapter`。
- 约束：课堂场景稳定性优先，WPF 渲染链路与现有功能保持可回滚。

## 2. 当前架构与目标差距
- 当前优点：
  - 已有 `Domain/Services/Infra/Interop/App` 五项目拆分。
  - Interop 风险区已独立，测试基础较完整。
- 主要差距：
  - `Services` 同时承担应用编排与部分领域/技术细节，职责混杂。
  - `App` 中仍包含较多流程编排与用例逻辑（非纯 UI 适配）。
  - 端口/适配器契约不统一，跨层依赖方向不够严格。
  - 模块级边界（RollCall / Paint / Photos / Presentation）缺少统一治理。

## 3. 目标形态（逻辑结构）
- `ClassroomToolkit.App`：
  - WPF 视图、ViewModel、窗口编排、输入绑定。
  - 只调用 Application UseCase，不直连 Infra/Interop 实现。
- `ClassroomToolkit.Application`（新增）：
  - 用例编排、DTO、端口接口（Repository/Gateway/Clock/Telemetry）。
  - 事务边界、策略装配、跨模块工作流。
- `ClassroomToolkit.Domain`：
  - 纯业务规则、聚合、值对象、领域服务。
  - 不依赖 App/Infra/Interop。
- `ClassroomToolkit.Infrastructure`（现 Infra）：
  - 文件/配置/持久化/日志等实现。
  - 实现 Application 端口。
- `ClassroomToolkit.Interop.Adapter`（由现 Interop 承担）：
  - Win32/COM/WPS 适配实现。
  - 对外暴露稳定接口，隐藏平台细节。

## 4. 迁移原则
- 小步快跑：每期可独立发布、可回滚。
- 依赖单向：UI -> Application -> Domain；Infrastructure/Interop 仅向上实现端口。
- 先契约后搬迁：先抽接口与 DTO，再迁移实现。
- 行为不变优先：先保证结果一致，再做内部优化。

## 5. 分阶段执行计划

### Phase 0：基线冻结（1 周）
- 任务：
  - 固化关键回归：现有 `Brush/Interop/Presentation` 全量测试门禁。
  - 记录性能与稳定性基线（冷启动、输入延迟、GC、崩溃率）。
- 交付：
  - `docs/validation/architecture-baseline-*.md`
  - CI 门禁项（Debug + Release）

### Phase 1：建立 Application 层（1~2 周）
- 任务：
  - 新建 `src/ClassroomToolkit.Application` 项目。
  - 定义核心端口接口（示例）：
    - `ISettingsStore`
    - `IStudentRepository`
    - `IPresentationGateway`
    - `IInkStorageGateway`
    - `ITelemetrySink`
  - 将“用例入口”抽到 Application（先选 1~2 条主流程）。
- 交付：
  - Application 项目可被 App 引用并跑通主流程。
  - 不改 UI 行为。

### Phase 2：按模块迁移编排逻辑（2~4 周）
- 模块顺序（风险从低到高）：
  1. Photos
  2. RollCall
  3. Paint（不改渲染算法，仅搬编排）
  4. Presentation/Interop 协调
- 每个模块步骤：
  - 提炼 UseCase（命令/查询）。
  - App 中流程逻辑下沉到 Application。
  - Infra/Interop 提供端口实现。
  - 补模块契约测试与回归测试。
- 交付：
  - `App` 中业务编排明显收敛。
  - 模块依赖图满足单向依赖。

### Phase 3：边界治理与清理（1~2 周）
- 任务：
  - 删除绕过 Application 的直连调用。
  - 统一异常策略与降级策略（尤其 Interop 忙/失败场景）。
  - 清理历史重复服务与临时适配层。
- 交付：
  - 架构守卫（可用测试/脚本）：
    - 禁止 `App -> Infra` 直接依赖（除 Composition Root）
    - 禁止 `Domain` 引用 `App/Infra/Interop`

### Phase 4：验收与冻结（1 周）
- 验收维度：
  - 功能一致性：关键场景回归通过。
  - 性能：输入延迟、GC、CPU 不劣化（阈值 < 10%）。
  - 稳定性：Interop 异常降级路径覆盖。
- 交付：
  - 架构迁移验收报告
  - 回滚手册（按 Phase 粒度）

### 每个 Phase 的 DoD（Definition of Done）模板
- 模板字段（每 Phase 必填）：
  - 目标与范围：本阶段覆盖模块、明确不覆盖项。
  - 代码清单：新增/修改项目与目录。
  - 行为一致性证据：关键用例回归截图/日志/测试编号。
  - 性能对比：迁移前后 `启动时间/输入延迟/GC` 对比表。
  - 稳定性证据：异常注入与降级验证结果。
  - 回滚信息：开关、命令、预期回滚时长。
  - 风险签字：Owner + Reviewer + 验收日期。

## 6. 代码改造清单（首批）
- 新增：
  - `src/ClassroomToolkit.Application/ClassroomToolkit.Application.csproj`
  - `src/ClassroomToolkit.Application/Abstractions/*`
  - `src/ClassroomToolkit.Application/UseCases/*`
- 调整：
  - `src/ClassroomToolkit.App/*`：移除用例编排到 Application
  - `src/ClassroomToolkit.Infra/*`：实现 Application 端口
  - `src/ClassroomToolkit.Interop/*`：实现网关/适配端口
  - `tests/ClassroomToolkit.Tests/*`：新增用例测试与架构守卫测试

## 6.1 迁移 Backlog（模块级）
| 模块 | Owner | 预计 PR 数 | 风险等级 | 目标里程碑 | 备注 |
| --- | --- | ---: | --- | --- | --- |
| Photos | 待指定 | 2-3 | 低 | M2 | 先迁查询与导航编排 |
| RollCall | 待指定 | 3-4 | 中 | M2 | 涉及计时与状态机 |
| Paint | 待指定 | 4-6 | 高 | M3 | 不改渲染算法，仅迁编排 |
| Presentation/Interop | 待指定 | 4-6 | 高 | M3 | 重点验证降级与重试 |

说明：
- 每个 PR 只做一类迁移动作（抽端口 / 迁 UseCase / 适配实现 / 清理旧路径）。
- 任何高风险 PR 必须附带回滚步骤与对照测试结果。

## 6.2 Architecture Guard（落地细则）
- Guard A：项目引用守卫
  - 规则：`ClassroomToolkit.Domain` 不得引用 `App/Infra/Interop`。
  - 规则：`ClassroomToolkit.App` 不得直接引用 `Infra/Interop`（Composition Root 豁免点除外）。
- Guard B：命名空间守卫
  - 规则：`App` 命名空间不得直接依赖 `Infra.*` 与 `Interop.*` 具体实现命名空间。
  - 规则：跨模块调用仅通过 `Application.UseCases` 与 `Application.Abstractions`。
- Guard C：CI 失败条件
  - 任一守卫失败直接阻断合并。
  - 任一 Phase 必跑测试失败直接阻断合并。
- 建议实现：
  - 在 `tests/ClassroomToolkit.Tests` 新增 `ArchitectureDependencyTests`。
  - 使用反射扫描引用与命名空间依赖，输出违规列表。

## 7. 风险与回滚
- 风险：
  - UI 流程迁移时出现行为偏差。
  - Interop 异步/重试策略在新边界下被破坏。
- 缓解：
  - 每 Phase 保持“旧路径可开关回退”。
  - 主路径双跑对比（旧服务 vs 新 UseCase）一段时间。
- 回滚：
  - 按 Phase 回滚到上一个稳定标签，不跨 Phase 回滚。

## 7.1 切换 Playbook（Cutover）
- Feature Flag 约定：
  - `AppFlags.UseApplicationUseCases`
  - `AppFlags.UseApplicationPhotoFlow`
  - `AppFlags.UseApplicationPaintFlow`
  - `AppFlags.UseApplicationPresentationFlow`
- 切换流程：
  1. 内部灰度（开发/测试环境）开启新路径并双跑日志对比。
  2. 小范围课堂试点（<=10% 场景）观察 3-5 天。
  3. 全量切换后保留旧路径 1 个发布周期。
- 观测指标与阈值：
  - 错误率：不得高于基线 10%。
  - 输入延迟 p95：不得高于基线 10%。
  - GC 峰值频率：不得高于基线 10%。
- 回退触发：
  - 任一核心指标连续 2 个观察窗口超阈值即回退。
  - 出现课堂阻断级故障（崩溃/卡死/控制失效）立即回退。

## 8. 验证策略
- 必跑：
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - `dotnet build ClassroomToolkit.sln -c Release`
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`
- 增量守卫：
  - 架构依赖测试（命名空间/项目引用约束）。
  - 模块契约测试（Application 端口 mock 验证）。

## 8.1 Phase 验收记录建议目录
- `docs/validation/phase-0-baseline.md`
- `docs/validation/phase-1-application-bootstrap.md`
- `docs/validation/phase-2-module-migrations.md`
- `docs/validation/phase-3-boundary-hardening.md`
- `docs/validation/phase-4-final-acceptance.md`

## 9. 里程碑与退出条件
- M1：Application 项目上线并承载至少 2 条主流程。
- M2：Photos/RollCall 完成迁移且无行为回归。
- M3：Paint/Presentation 编排迁移完成，性能不劣化。
- M4：清理旧路径，架构守卫全绿，进入维护期。

## 10. ADR 索引（防漂移）
- ADR-001：为何采用 Modular Monolith + Clean，而非重写或微服务。
- ADR-002：Application 端口定义与跨模块调用原则。
- ADR-003：Interop 适配层异常与降级策略。
- ADR-004：Feature Flag 切换与双跑策略。
- ADR-005：架构守卫规则与 CI 阻断策略。

建议：
- 每完成一个里程碑至少落地 1 篇 ADR。
- ADR 与 Phase 验收文档互相引用，保证决策可追踪。
