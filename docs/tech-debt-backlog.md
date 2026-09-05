# ClassroomToolkit 当前技术债

最后更新：2026-09-05

这里只记录仍未关闭、收益明确的问题；已完成任务从 Git 历史查询，不在当前 backlog 重复保留。验证层级与命令见根 [AGENTS.md](../AGENTS.md) 与 [README](../README.md)，此处不再复述。

## P2

- App 的 Paint/Windowing 仍有较多单调用者短 policy；只在触及对应路径时按 deletion test 局部内联，不启动全仓批量合并，也不删除有生产 adapter + 测试 adapter 的真实 Interop seam。
- xUnit 4、测试平台传递链和 SixLabors.Fonts 3.x 属于 major 迁移，当前 waiver 到期日为 2026-10-15；到期前需分别完成测试发现/CI 与字体/工作簿视觉兼容切片。
- Sqlite 业务存储实验链（adapter/bridge/capability 反射探测，约 600 行）默认双环境变量门关闭；组合根重构收口后按 ADR-004“不允许长期双跑”裁决：转正或整体裁剪。

## P3

- WPS 后台派发生命周期已有行为 seam，4 个字符串契约已退役；剩余源码形状契约主要保护 Win32 native unhook、阻塞等待和少量尚无可注入 seam 的安全分支，只在建立对应行为证据后局部退役。
- `GlobalHookService.HookUnavailable` 事件无生产订阅者（仅测试订阅并断言隔离行为）；退役需连同 `GlobalHookServiceLifecycleContractTests`（Gate=CoreContract）一起评估，属 hook 生命周期高风险切片。

## 暂缓

- 日志基础设施替换、线程模型重写、存储格式迁移：没有独立故障或现场数据前不启动。
- 约 117 处仅 `Debug.WriteLine` 的非致命 catch 未接 ILogger：出现现场排障需求时统一接 FileLoggerProvider；属于可观察性增强，不属于删减范畴。
