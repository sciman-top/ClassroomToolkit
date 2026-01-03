# project_rules.md (Project-Level) — ClassroomToolkit (WPF / .NET 8)

> 目标：为 ClassroomToolkit 提供项目级“可执行规则”：工程边界、目录职责、必跑命令、调试要点与课堂降级策略。

## 0) 项目概要
- 项目名：**ClassroomToolkit**
- 类型：WPF 桌面应用（教学辅助工具）
- 目标：点名、计时、屏幕画笔标注/形状绘制、白板、PowerPoint/WPS 翻页控制等课堂场景。

## 1) 技术栈与运行环境（硬约束）
- .NET 8.0、WPF（含 Windows Forms 互操作）
- Win32 / PInvoke 互操作
- Excel 读写：ClosedXML（学生名册）
- 测试：xUnit（可选 FluentAssertions）
- 运行环境：**Windows**（WPF 必需）

## 2) 工程结构与模块边界（必须遵守）
- `src/ClassroomToolkit.App/`：WPF UI、XAML、窗口与画笔逻辑（输入/渲染/交互）
- `src/ClassroomToolkit.Domain/`：核心业务逻辑（RollCallEngine、TimerEngine 等；尽量不依赖 UI/Interop）
- `src/ClassroomToolkit.Services/`：应用服务层（演示控制策略、协调 Domain/Interop）
- `src/ClassroomToolkit.Interop/`：Win32/WPS/PowerPoint 互操作（PInvoke/Automation 边界）
- `src/ClassroomToolkit.Infra/`：持久化与设置（INI、Excel）

边界规则：
- 业务逻辑尽量下沉到 Domain；UI 层避免堆业务规则。
- Interop 不应渗透进 Domain；通过 Services/接口隔离。

## 3) 必跑命令（自动执行，完成改动后必须跑）
> 能跑必跑；跑不了必须说明原因并给替代验证方式。

- 依赖恢复：`dotnet restore`
- 构建：`dotnet build ClassroomToolkit.sln`
- 测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- 运行（smoke）：`dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`

建议（如已配置）：`dotnet format`（或仓库现有格式化流程）。

## 4) 编码风格与约定
- 缩进：4 空格；遵循现有 C# 风格
- 命名：
  - 类/方法：PascalCase
  - 字段/局部变量：camelCase
  - 常量：UPPER_SNAKE_CASE
- Win32 互操作：
  - P/Invoke 尽量集中在 Interop 层或明确的 native 边界静态类中
  - 常量使用 `const int`
  - 优先沿用仓库既有封装方式（如 SafeHandle/包装类已存在则复用）
- 变更策略：最小改动（minimal diff），避免无关重构与大范围格式化。

## 5) 用户数据与资源保护（强约束）
- 避免破坏/覆盖：
  - `students.xlsx`（学生名册）
  - `student_photos/`（照片资源）
- 禁止在未明确指令下：
  - 重命名/删除/批量改写上述文件或目录
  - 改变 Excel 文件结构（列/表名/格式）导致兼容性破坏
- `settings.ini` 为运行时生成：如需写入，建议“写临时文件 → 原子替换”避免半写损坏。

## 6) 错误处理与课堂降级策略（产品级要求）
- 关键路径需避免崩溃：点名/计时/画笔/演示控制/Excel/互操作调用。
- 外部边界（Excel、语音、Automation、PInvoke、文件 I/O）：
  - 使用局部 try/catch（避免全局吞异常）
  - 关键失败：MessageBox 提示 + 可执行建议（例如：文件被占用、未打开演示、权限不足）
  - 能降级就降级（不中断课堂优先），但不得“假装成功”。

## 7) 调试规范与技巧（高频问题）
### 7.1 焦点/窗口/鼠标穿透
- 使用 Spy++ / Inspect 等工具检查：
  - `GetForegroundWindow()` 与预期是否一致
  - 窗口扩展样式：`WS_EX_NOACTIVATE` / `WS_EX_TRANSPARENT` 是否导致输入异常
- WinForms/WPF 互操作输入问题（如 Alt 系统键）优先从消息路径与预处理逻辑定位，避免用“魔法延时”掩盖问题。

### 7.2 演示控制（PowerPoint / WPS）
- 开启诊断日志：检查 `PresentationClassifier` 分类结果与输入发送策略（键盘/消息/自动化）是否匹配当前演示应用。
- 优先保证“不误触发、不乱翻页”；失败时提示并允许手动操作。

### 7.3 性能（画笔/绘制）
- 绘制优先使用 WriteableBitmap 等高效路径（以仓库现有实现为准）。
- 高频事件（Move/Stroke）避免阻塞 I/O、大对象分配、频繁创建 Brush/Pen（可冻结则冻结）。

## 8) 交付与验收（DoD）
每次任务完成至少满足：
- 代码已落盘（不留占位符）
- 必跑命令已执行并汇报结果（或说明无法执行原因与替代验证）
- 改动点可追溯（按文件列出）
- 不破坏 students.xlsx / student_photos/，不引入破坏性操作
