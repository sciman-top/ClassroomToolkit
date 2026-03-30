# ClassroomToolkit 终态最佳架构总方案（唯一执行口径）

最后更新：2026-03-10  
状态：active

## 1. 目标

- 彻底重构到“终态最佳架构”。
- 保持全功能零回归。
- 课堂主链稳定优先于形式层升级。

## 2. 固定终态（已确认，不再反复改方向）

- UI 主体：`WPF + .NET LTS`（当前已升至 `.NET 10`）。
- 运行时：`状态机中心化 + 策略函数化 + 定向回归矩阵`。
- 存储：`JSON 配置 + SQLite 业务数据`。
- 系统边界：`Interop Adapter 隔离（超时 / 重试 / 降级 / 日志）`。
- Ink：`IInkRenderer 抽象（CPU 保底 + GPU 可选）`。

### 2.1 终态层次口径

- `App`：WPF Shell、DI 组合根、View/ViewModel、Session、Windowing、受控 Adapter/Executor。
- `Application`：用例、端口、跨模块编排。
- `Domain`：纯规则、模型、算法。
- `Services`：运行时能力实现层 / Application 端口实现外观层；不是第二编排中心。
- `Infra`：配置、迁移、持久化、日志。
- `Interop`：Win32/COM/PInvoke/WPS 等高风险外部接入。

说明：

- 详细边界以 `docs/architecture/2026-03-10-target-boundary-map.md` 为准。
- `Services` 当前不再视为待扩张层，只允许承接运行时能力实现，不得继续吸收 UI 场景编排。

## 3. 非目标（当前阶段明确不做）

- 不为“视觉更现代”而迁移到 WinUI 3。
- 不做无验证的大改写。
- 不在高风险主链未稳定前引入新的跨层抽象或大范围公共 API 改写。
- 不把 GPU Ink 作为发布阻塞项；GPU 属于可选增强项，不得反向拖慢 CPU 稳定主链。

## 4. 完成定义（Definition of Done）

### 4.1 运行时主链完成定义

- 图片 / PDF / 白板 / PPT-WPS 四场景互切不再依赖散点状态修补。
- 启动器 / 工具条 / 点名窗口 / Overlay / ImageManager 的层级关系由统一协调路径处理。
- 焦点、输入穿透、翻页、光标/画笔切换遵循统一 Session / Policy / Executor 链路。
- 高风险链路不存在“必须拖动或缩放后才刷新”的已知缺陷。

### 4.2 存储完成定义

- 配置默认写入 JSON，INI 仅保留迁移与兼容兜底。
- RollCall 之外的班级 / 历史等业务数据完成 SQLite 落库与读取路径切换。
- 数据迁移具备失败降级、兼容回读和最小回滚策略。

### 4.3 Interop 边界完成定义

- App 层不再新增直接依赖 `ClassroomToolkit.Interop` 的 UI / 场景编排文件。
- Interop 调用集中在 Adapter / Executor / Windowing 边界。
- 关键 Interop 路径统一具备超时、重试、降级、日志。
- `ArchitectureDependencyTests` 守卫与白名单与现状一致，且只允许收紧，不允许扩散。

### 4.4 Ink 完成定义

- `IInkRenderer` 成为唯一渲染抽象入口。
- CPU 路径达到稳定可发布状态。
- GPU 路径具备明确启用开关、能力探测和失败回退，不影响 CPU 主路径。

说明：

- 当前代码已经具备统一渲染入口骨架，但 GPU 仍为占位回退实现，尚未达到“独立 GPU 发布路径”。

### 4.5 验证完成定义

- 每批改动至少通过定向测试与全量 Debug 测试。
- 高风险批次补跑全量 Release 测试。
- 冻结验收阶段补做人工场景回归：
  - PPT / WPS 全屏放映
  - 图片 / PDF 全屏跨页
  - 白板
  - 三者互切

### 4.6 文档完成定义

- 主方案、主进度、handover、边界图、Interop 台账、回滚手册口径一致。
- 历史 `phase-*` 文档只保留历史结论，不再承担当前执行职责。
- 任何边界变化都必须同步到守卫说明与台账。

## 5. 当前剩余执行主线（按优先级）

1. 高风险运行时主链继续收口  
场景互切、窗口层级、焦点与输入、跨页墨迹并发。

2. Interop 边界继续压缩  
把 `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 中残余高风险直连上收到 Adapter / Session / Updater。

3. 存储分治继续深化  
从配置默认 JSON + RollCall SQLite，推进到完整业务数据 SQLite。

4. Ink 抽象收尾  
维持 CPU 稳定主路径，补齐 GPU 可选启用条件、探测与回退。

5. 最终冻结验收  
文档、守卫、测试、人工回归口径统一后进入冻结。

## 6. 当前硬指标（用于指导后续收口）

- 目标框架：已统一到 `.NET 10`。
- App 层直接引用 `ClassroomToolkit.Interop` 的文件数：当前基线为 `6`，后续只允许下降。
- 当前全量自动化验证基线：Debug `1908/1908`、Release `1908/1908`。

### 6.1 当前结构现实

- `App` 仍直接引用 `Application / Infra / Services / Interop` 项目。
- 其中 `App -> Interop` 的 `6` 个文件属于“历史债务 + 少量稳定边界”，不是终态许可扩张面。
- 逐文件归宿以 `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md` 为准。

### 6.2 当前 Feature Flag 现实

- 有效切换类开关聚焦于 `SQLite` 与 `GPU Ink`。
- `CTOOLKIT_USE_APPLICATION_*` 目前不再作为可靠的主链回滚手段，不得写入新的冻结/回滚口径。

## 7. 执行规则

- 文档裁决优先级固定：主方案 > 主进度 > handover > ADR > phase 历史文档。
- `phase-*` 文档仅作历史参考，不得推翻当前口径。
- 默认连续执行，不做阶段性暂停。
- 新增功能或行为修改前，先判定归宿层；禁止为赶进度继续向 `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 堆叠运行时分支。
- 热点窗口文件只允许保留视图接线与轻量协调；新增状态写回、Interop 决策、存储决策、场景编排默认外提到 `Session / Policy / Updater / Executor / UseCase / Infra`。
- `Services` 不得回流为第二编排中心；新增业务规则与场景互切逻辑默认不进入 `Services`。
- 仅在以下情况暂停：
  - 主方案 / 主进度 / handover / ADR 之间按优先级仍无法裁决
  - 必须外部人工条件或外部环境
  - 无法建立最小验证闭环
  - 破坏性迁移缺少回滚定义

### 7.1 文档同步规则

- 只要改动以下任一项，必须同步更新主方案 / 主进度 / handover：
  - 总进度
  - App -> Interop 基线
  - 有效 Feature Flag
  - 最终验收入口
  - 层职责与 `Services` 定位
- 若涉及边界变化，还必须同步更新：
  - `docs/architecture/2026-03-10-target-boundary-map.md`
  - `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md`
  - `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs` 的说明口径

## 8. 文档关系

- 主方案：本文档
- 主进度：`docs/validation/2026-03-06-target-architecture-progress.md`
- 交接：`docs/handover.md`
- 首批编码切片计划：`docs/plans/2026-03-10-target-architecture-first-batch-implementation-plan.md`
- 终态边界图：`docs/architecture/2026-03-10-target-boundary-map.md`
- Interop 债务台账：`docs/architecture/2026-03-10-interop-direct-dependency-matrix.md`
- 最终验收清单：`docs/validation/target-architecture-final-acceptance.md`
- ADR：约束与裁决依据
- `docs/validation/phase-*.md`：历史阶段记录，保留但不再作为当前执行口径
