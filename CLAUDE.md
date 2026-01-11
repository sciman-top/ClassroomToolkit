# CLAUDE.md — ClassroomToolkit (Project-Level)

## 0. 目的
本文件为 ClassroomToolkit 仓库提供**项目级**约束与工作流：覆盖/补充用户级规则，避免 agent 在 WPF/互操作/外部集成场景中“猜测式修复”与扩大改动面。

## 1. 项目概要
- Windows WPF 教学辅助工具：点名、计时、全屏画笔/形状标注与白板、PowerPoint/WPS 翻页控制。
- 核心原则：课堂场景优先（稳定、可恢复、可降级），避免阻塞教学流程。

## 2. 语言策略
- 默认：尽量使用简体中文解释与沟通。
- 保留英文原文的情况：错误信息/日志、API/命令/配置键/标识符/路径等，避免翻译带来歧义。
- 代码注释：English（Why-comments）。

## 3. 工程优先级
1) 正确性与稳定性（不崩溃、可恢复、可降级）
2) 最小改动（minimal diff）、可回滚
3) 可维护性（清晰边界：App/Domain/Services/Interop/Infra）
4) 性能（只在有证据的 hot path 优化）

## 4. 关键目录与职责边界
- App：WPF UI / XAML / 输入与画笔交互
- Domain：纯业务逻辑（RollCallEngine、TimerEngine）
- Services：应用服务（演示控制策略等）
- Interop：Win32/PInvoke、WPS/PowerPoint 互操作
- Infra：配置/持久化（INI、Excel）

> 约束：业务逻辑优先下沉到 Domain；UI 只做交互与绑定；Interop 不渗透到 Domain。

## 5. 必跑命令（能跑必跑）
- `dotnet restore`
- `dotnet build ClassroomToolkit.sln`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- `dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`（至少做一次 smoke）

若无法运行（环境/权限/缺少依赖），需明确说明原因，并给出替代验证步骤。

## 6. 变更边界（严格）
- 不做无关重构；不做大范围格式化；不“顺手清理”。
- 不改变公共 API、数据格式、持久化结构、用户交互流程，除非明确要求。
- 不破坏用户数据与资源：
  - `students.xlsx`
  - `student_photos/`
  - `settings.ini` 为运行时生成，写入需避免半写/损坏（建议临时文件+原子替换）。

## 7. 错误处理与降级（课堂友好）
- 外部边界（Excel/互操作/音频/Automation）必须防崩溃：
  - 局部 try/catch（避免大范围吞异常）
  - 关键失败：MessageBox 给出可执行建议
  - 能降级就降级（例如：演示控制失败时提示并允许手动）
- 不允许“吞掉异常并继续假装成功”。

## 8. 互操作（Win32 / PInvoke / WinForms 互操作）注意事项
- P/Invoke 声明集中在 Interop 层；遵循仓库既有组织方式。
- 对焦点/穿透/输入问题：优先从窗口样式与消息路径定位（Spy++、GetForegroundWindow 等）。
- 对 WinForms/WPF 输入差异（如 Alt 系统键）：优先确认消息预处理与转发路径，不要用“魔法延时”掩盖问题。

## 9. 输出要求（每轮响应）
- 计划（checklist，简短）
- 修改点（按文件列出，“为什么这样改”）
- 验证（命令 + 结果）
- 风险/后续（必要时）

## 10. WPS 演示特殊说明
WPS 的 COM 自动化接口与 PowerPoint 存在显著差异，开发时需注意：

- **手动放映不可追踪**：用户按 F5 启动放映时，`Application.SlideShowWindows.Count` 始终返回 0，外部进程无法获取当前页码。
- **推荐启动方式**：通过工具栏【开始放映】按钮调用 `SlideShowSettings.Run()` 启动，才能启用按页笔迹缓存。
- **降级模式**：若检测到 WPS 放映但无法读取页码，自动降级为"会话级画布"（不按页清除笔迹）。
- **版本差异**：
  - ProgID：优先 `KWPP.Application`，回退 `WPP.Application`
  - 进程名：`wpp.exe` 或 `wps.exe`（因版本而异）
- **个人版限制**：WPS 个人版可能阉割 COM 自动化能力，即使调用 `Run()` 也可能不创建 SlideShowWindow。

## 11. 调试速查
| 问题类型 | 排查方法 |
|---------|---------|
| 焦点/穿透 | Spy++ 查看窗口样式；检查 `WS_EX_NOACTIVATE`、`WS_EX_TRANSPARENT` |
| WPS 放映检测 | 检查 `SlideShowWindows.Count`；查看 `[WpsTracker]` 开头的 Debug 输出 |
| 笔迹缓存 | 检查 `_currentCacheKey`；查看 `[InkCache]` 开头的 Debug 输出 |
| COM 连接 | 运行 `find_wps_progid.ps1` 验证 ProgID 注册情况 |
| F5 拦截 | 查看 `[F5Interceptor]` 开头的 Debug 输出；检查 UIPI 权限差异 |

## 12. F5 拦截模块（实验性）
为解决"手动 F5 放映无法追踪"问题，新增以下模块：

- **F5InterceptorController**：全局 WH_KEYBOARD_LL 钩子，当 WPS 前台时拦截 F5/Shift+F5，转为 COM Run()
- **WpsForegroundDetector**：检测前台窗口是否属于 WPS 进程，并检测 UIPI 权限限制
- **OleMessageFilter**：处理 COM RPC_E_CALL_REJECTED，自动重试繁忙的 COM 调用
- **StartSlideshowFromCurrentSlide**：支持 Shift+F5 从当前页放映

**注意事项**：
- 钩子回调必须极轻量（<10ms），COM 调用通过 Dispatcher 异步执行
- UIPI 限制：若 WPS 以管理员运行而本程序非管理员，拦截可能无效，自动降级
- **当前状态：已禁用**。全局键盘钩子会导致绘图卡顿，需要进一步优化（考虑改用 RegisterHotKey 或仅在 WPS 进程存在时启用）

