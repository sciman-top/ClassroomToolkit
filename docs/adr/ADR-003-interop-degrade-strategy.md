# ADR-003: Interop 适配层异常与降级策略

- 日期: 2026-02-24
- 最后更新: 2026-03-10
- 状态: Accepted

## 背景

高风险链路仍包含大量窗口、输入、放映控制、焦点和 Z-Order 相关的 Interop 行为。若边界不清晰，会持续把异常、重试与时序问题泄漏到 UI 场景编排层。

## 决策

- Interop 失败不向 UI 抛出致命异常。
- Presentation 控制保留自动降级（Raw -> Message）策略。
- Application 用例不直接依赖 Interop 类型。
- App 层直接 Interop 依赖只允许继续收敛到 Adapter / Executor / Windowing 边界，不允许继续扩散到新的 UI / 场景编排文件。
- 关键 Interop 路径应统一具备超时、重试、降级、日志。

## 当前执行约束

- `MainWindow.*`、`PaintOverlayWindow.*`、`RollCallWindow.*` 中残余直连属于历史债，应继续上收，不视为可复制模式。
- 允许保留的稳定边界优先是 `Windowing` 目录下的 Adapter / Executor 文件。
- 任何新增 Interop 接入，默认优先新增边界适配器，而不是在现有 UI 文件内直接调用。

## 已落地

- Presentation 发送通过 `SendPresentationCommandUseCase` -> `PresentationGateway` 转发。
- 部分前台演示、窗口样式位、句柄校验已切到 App 内枚举或 Windowing Adapter。
