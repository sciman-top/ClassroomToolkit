# GEMINI.md — ClassroomToolkit (Project-Level)

## 0. 项目概览
ClassroomToolkit 是 Windows WPF 教学辅助工具，支持：
- 随机点名（分组、照片展示）
- 计时器/秒表/时钟
- 全屏画笔与形状绘制、白板模式
- PowerPoint/WPS 翻页控制与输入策略

## 1. 语言策略
- 默认：尽量使用简体中文解释与沟通。
- 保留英文原文：错误信息/日志、API/命令/配置键/标识符/路径等，避免翻译误差。
- 代码注释：English。

## 2. 技术栈与结构
- .NET 8.0 + WPF（含 WinForms 互操作）
- Win32/PInvoke
- ClosedXML（Excel）
- xUnit（可选 FluentAssertions）
- 目录：
  - `src/ClassroomToolkit.App/`
  - `src/ClassroomToolkit.Domain/`
  - `src/ClassroomToolkit.Services/`
  - `src/ClassroomToolkit.Interop/`
  - `src/ClassroomToolkit.Infra/`

## 3. 必跑命令（完成改动后）
- `dotnet restore`
- `dotnet build ClassroomToolkit.sln`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- `dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`

## 4. 变更边界（严格）
- 最小改动（minimal diff），不做无关重构与大范围格式化。
- 不改变公共 API、数据格式、持久化结构与用户交互流程，除非明确要求。
- 保护用户数据与资源：不要破坏/覆盖 `students.xlsx`、`student_photos/`；`settings.ini` 写入需避免半写损坏。

## 5. 错误处理与课堂降级
- 外部边界（Excel、互操作、音频、Automation）使用局部 try/catch，避免崩溃。
- 关键失败：MessageBox 提示 + 可执行建议（文件占用、未打开演示、权限不足等）。
- 能降级就降级，优先不中断课堂。

## 6. 调试速查
- 焦点/窗口：Spy++，核对 GetForegroundWindow()；检查 WS_EX_NOACTIVATE / WS_EX_TRANSPARENT。
- 演示控制：启用诊断日志；核对 PresentationClassifier 分类与输入发送。
- 性能：绘制优先 WriteableBitmap；高频事件避免重操作；Brush/Pen 可冻结则冻结。

## 7. 输出格式
- Plan（checklist）
- Changes（files + rationale）
- Verification（commands + results）
- Risks / follow-ups（必要时）
