# ClassroomToolkit 当前技术债

最后更新：2026-08-17

这里只记录仍未关闭、收益明确的问题；已完成任务从 Git 历史查询，不在当前 backlog 重复保留。

## P2

- App 的 Paint/Windowing 仍有较多单调用者短 policy；只在触及对应路径时按 deletion test 局部内联，不启动全仓批量合并，也不删除有生产 adapter + 测试 adapter 的真实 Interop seam。
- xUnit 4、测试平台传递链和 SixLabors.Fonts 3.x 属于 major 迁移，当前 waiver 到期日为 2026-10-15；到期前需分别完成测试发现/CI 与字体/工作簿视觉兼容切片。

## P3

- WPS 后台派发生命周期已有行为 seam，4 个字符串契约已退役；剩余源码形状契约主要保护 Win32 native unhook、阻塞等待和少量尚无可注入 seam 的安全分支，只在建立对应行为证据后局部退役。

## 暂缓

- 日志基础设施替换、线程模型重写、存储格式迁移：没有独立故障或现场数据前不启动。

## 验证原则

- 普通修复：受影响测试 + build。
- 共享/高风险 seam：standard。
- 依赖变化或发布：full。
- 不重复运行已被更强且精确当前结果覆盖的层。
