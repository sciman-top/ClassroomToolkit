# ClassroomToolkit 当前技术债

最后更新：2026-10-26

这里只记录仍未关闭、收益明确的问题；已完成任务从 Git 历史查询，不在当前 backlog 重复保留。验证层级与命令见根 [AGENTS.md](../AGENTS.md) 与 [README](../README.md)，此处不再复述。

## P2

- Paint/Windowing 已完成多轮按职责合并微策略与死 API 的减负；后续仅在实际触及处按删除测试评估，不再启动全仓批量合并，也不删除有生产 adapter + 测试 adapter 的真实 Interop seam。
- xUnit 4、测试平台传递链和 SixLabors.Fonts 3.x 属于 major 迁移，当前 waiver 到期日为 2027-01-15；到期前需分别完成测试发现/CI 与字体/工作簿视觉兼容切片。
- Sqlite 业务存储实验链（adapter/bridge/capability 反射探测，约 600 行）默认双环境变量门关闭；组合根重构收口后按 ADR-004“不允许长期双跑”裁决：转正或整体裁剪。

## P3

- WPS 后台派发生命周期已有行为 seam，InteropAnalyzerContractTests 已整体退役；Interop 侧仅保留 Unicode marshalling 行为契约（InteropMarshallingContractTests），无剩余源码形状契约。

## 暂缓

- 日志基础设施替换、线程模型重写、存储格式迁移：没有独立故障或现场数据前不启动。
- 约 117 处仅 `Debug.WriteLine` 的非致命 catch 未接 ILogger：出现现场排障需求时统一接 FileLoggerProvider；属于可观察性增强，不属于删减范畴。
