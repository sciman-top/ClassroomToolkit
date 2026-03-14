# 终态目标边界图（唯一边界口径）

最后更新：2026-03-10  
状态：active  
对应主方案：`docs/plans/2026-03-06-best-target-architecture-plan.md`

## 1. 目的

- 把“终态最佳架构”收紧为可执行边界，而不是只保留方向性描述。
- 明确 `App / Application / Domain / Services / Infra / Interop` 的最终职责、允许依赖与禁止行为。
- 为后续收口、守卫和文档同步提供统一裁决依据。

## 2. 终态层次与职责

| 层 / 项目 | 终态职责 | 允许依赖 | 明确禁止 |
| --- | --- | --- | --- |
| `ClassroomToolkit.App` | WPF Shell、DI 组合根、View/ViewModel、Session 协调、Windowing 编排、用户交互适配 | `Application`、有限的 `Infra` 组合根接入、有限的 `Services` 运行时接入、受控的 `Interop` 边界文件 | 新增业务规则、新增散点状态修补、新增普通 UI 文件直连 Interop |
| `ClassroomToolkit.Application` | 用例、跨模块编排、端口抽象、应用级契约 | `Domain` | 依赖 `App` / `Infra` / `Interop`，以及承载 UI 时序细节 |
| `ClassroomToolkit.Domain` | 纯业务模型、规则、序列化、算法、计时器 | 无外层依赖 | 引入 UI、存储、Interop、日志框架耦合 |
| `ClassroomToolkit.Services` | 运行时服务与应用端口实现外观层；承接 Presentation/Input/Speech 等“非持久化能力”的实现 | `Application`、`Domain`、`Interop` | 变成新的业务中心；承载 UI 场景编排；扩散为第二个 App |
| `ClassroomToolkit.Infra` | 配置、JSON/INI 迁移、SQLite/Workbook 持久化、日志等基础设施实现 | `Application`、`Domain` | 承载 UI 场景状态；新增交互时序逻辑 |
| `ClassroomToolkit.Interop` | Win32/COM/PInvoke/WPS/输入发送等高风险外部接入 | `Domain` 可无，通常独立 | 直接承载 UI 决策、业务策略、场景编排 |

## 3. `Services` 层的终态定位

- `Services` 不是待扩张的“第二编排层”。
- 当前终态口径下，`Services` 保留为“运行时能力实现层 / 应用端口实现外观层”。
- `Services` 允许继续存在，但职责必须被收窄到：
  - Presentation 控制能力实现
  - 输入/语音等运行时能力实现
  - 对 `Application` 端口的适配实现
- `Services` 不再承接以下新增内容：
  - UI 交互状态机
  - 场景互切编排
  - 业务规则与持久化策略

## 4. App 层允许保留的特殊边界

以下属于终态下允许保留在 `App` 层的受控职责：

- WPF 视图与 ViewModel
- `Session` 目录中的 UI 会话状态与 effect runner
- `Windowing` 目录中的窗口编排、Adapter、Executor、受控重试/降级
- 组合根中的 `Infra` / `Services` 注册
- 与 WPF 运行时强耦合的少量 UI 辅助代码

以下不属于允许保留的常态：

- 普通窗口代码后置中新增 `ClassroomToolkit.Interop` 依赖
- 在 `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 中继续累积跨层编排
- 通过 feature flag 长期并存两条高风险主链

## 5. 允许依赖矩阵（执行版）

| From | To | 结论 |
| --- | --- | --- |
| App -> Application | 允许，主路径 |
| App -> Domain | 允许，但仅限轻量模型/枚举；新增编排优先经过 Application |
| App -> Services | 允许，但仅限运行时服务接入，不得把 Services 当业务中心 |
| App -> Infra | 仅组合根和极少数历史组合点允许；后续只允许减少 |
| App -> Interop | 仅受控白名单允许；后续只允许减少 |
| Application -> Domain | 允许 |
| Application -> Infra / Interop / App | 禁止 |
| Services -> Application / Domain / Interop | 允许 |
| Infra -> Application / Domain | 允许 |
| Domain -> 其他外层 | 禁止 |

当前守卫白名单基线：App 层直接引用 `ClassroomToolkit.Interop` 文件数为 `6`（2026-03-10）。

## 6. 子系统归宿

### 6.1 Windowing / Session

- `Session` 负责“状态转移 + invariant + effect”。
- `Windowing` 负责“窗口层级 / Topmost / Owner / Focus / Retouch / Retry / 降级”。
- 目标：`MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 只保留视图协调，不继续堆叠状态写入和 Interop 细节。

### 6.2 Presentation

- 终态主路径应为：
  - `App` 发起交互
  - `Application.UseCases.Presentation` 表达意图
  - `Services.Presentation` 实现网关或运行时能力
  - `Interop` 执行底层输入/窗口控制
- 当前 `PaintOverlayWindow` 仍存在直连 `PresentationControlService` 的历史债，后续应继续上收，不作为可复制模式。

### 6.3 Storage

- 配置：`JSON` 为默认主路径，`INI` 仅兼容/迁移兜底。
- 业务数据：`SQLite` 为终态方向，Workbook/Excel 仅作兼容或导入来源，不再作为运行时主存储。

### 6.4 Ink

- 终态要求是“唯一 Ink 渲染入口”，当前代码对应为工厂入口与渲染器组合，而不是已完成的独立 GPU 实现。
- CPU 是发布主路径。
- GPU 仅为受控可选能力，必须具备：
  - 双重开关/能力探测
  - 明确失败回退
  - 不影响 CPU 主链

## 7. Feature Flag 口径

### 7.1 仍然有效的切换类开关

- `CTOOLKIT_USE_SQLITE_BUSINESS_STORE`
- `CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND`
- `CTOOLKIT_USE_GPU_INK_RENDERER`
- `CTOOLKIT_ENABLE_EXPERIMENTAL_GPU_INK`

### 7.2 不再作为回滚主手段的开关

- `CTOOLKIT_USE_APPLICATION_USECASES`
- `CTOOLKIT_USE_APPLICATION_PHOTO_FLOW`
- `CTOOLKIT_USE_APPLICATION_PAINT_FLOW`
- `CTOOLKIT_USE_APPLICATION_PRESENTATION_FLOW`

说明：

- 上述 `Application*Flow` 开关目前仅在 `AppFlags` 中保留定义，未形成可靠的运行时主链切换闭环。
- 后续文档与 runbook 不应再把这些开关当作“验收冻结回滚总闸”。
- 如需保留，应在未来单独 ADR 中说明其真实接线点；否则按技术债清理。

## 8. 文档同步规则

只要以下任一项发生变化，必须同步更新 5 份文档：

- 层职责
- `Services` 定位
- App -> Interop 允许边界
- 有效 feature flag
- 最终验收入口

同步目标：

- `docs/plans/2026-03-06-best-target-architecture-plan.md`
- `docs/validation/2026-03-06-target-architecture-progress.md`
- `docs/handover.md`
- 本文档
- `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md`
