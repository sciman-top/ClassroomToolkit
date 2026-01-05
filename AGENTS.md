# AGENTS.md — ClassroomToolkit (Project-Level)

> Scope: This file contains **project-specific** guidance for coding agents. User/global rules still apply unless explicitly overridden.  
> Context: Codex supports cascading `AGENTS.md` / `AGENTS.override.md` rules by directory. Keep this root file repo-wide and add narrower files only when needed.  

## 0. 项目概览
- 项目名：**ClassroomToolkit**
- 类型：**Windows WPF 桌面教学辅助工具**
- 目标场景：点名、计时、屏幕画笔/形状标注、白板、PowerPoint/WPS 翻页控制、笔迹记录/回看、图片教学等课堂交互。

## 1. 语言与表达
- 默认：**尽量使用简体中文**进行说明与沟通。
- 例外：当英文能显著减少歧义或必须保留原始信息时，保留英文原文（错误信息/日志、API 名称、命令、配置键、标识符、文件路径等）。
- 代码注释：**English**（Why-comments；避免“解释代码在做什么”的废话注释）。

## 2. 技术栈与运行约束
- .NET 8.0 + WPF（含 Windows Forms 互操作）
- Win32 / PInvoke 互操作（User32 等）
- Excel 读写：ClosedXML（学生名册）
- 测试：xUnit（可选 FluentAssertions）
- 运行环境：**Windows Only**（WPF 必需）

## 3. 目录结构（repo-wide）
- `src/ClassroomToolkit.App/`：WPF UI、XAML、窗口与画笔逻辑
- `src/ClassroomToolkit.Domain/`：核心业务逻辑（RollCallEngine、TimerEngine 等）
- `src/ClassroomToolkit.Services/`：应用服务层（演示控制策略）
- `src/ClassroomToolkit.Interop/`：Win32/WPS/PowerPoint 互操作
- `src/ClassroomToolkit.Infra/`：持久化与设置（INI、Excel）

## 4. 必跑命令（完成任何改动后）
> 目标：可复现、可回归。能跑就跑；不能跑要明确说明原因与替代验证方式。

- 还原依赖：`dotnet restore`
- 构建：`dotnet build ClassroomToolkit.sln`
- 运行：`dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
- 测试：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`

建议（如仓库已配置）：`dotnet format` / `dotnet test` 增量运行。
如因非 Windows 环境或依赖缺失无法执行，请明确写明原因与替代验证（例如仅 `dotnet build`）。

## 5. 编码规范与边界
### 5.1 代码风格
- 缩进：4 空格；遵循现有 C# 代码风格
- 命名：
  - 类/方法：PascalCase
  - 字段/局部变量：camelCase
  - 常量：UPPER_SNAKE_CASE
- 变更策略：**最小差异（minimal diff）**，避免无关重构与大范围格式化。

### 5.2 互操作（Win32 / PInvoke）约束
- P/Invoke 声明应集中在清晰的 interop 边界内（通常在 `ClassroomToolkit.Interop` 或特定 native library 对应的静态类中），避免分散在业务/界面层。
- 常量使用 `const int`；句柄优先使用 `SafeHandle`（如已有相关模式/封装则沿用）。
- 不要为“临时修复”引入宽泛的 catch-all 互操作包装；优先定位 root cause。

### 5.3 用户数据与资源的保护（强约束）
- **避免破坏/覆盖**：
  - `students.xlsx`（学生名册）
  - `student_photos/`（照片资源）
- 笔迹/截图/照片存储默认路径：`D:\ClassroomToolkit\Ink\`（照片可在画笔设置中自定义根目录）。
- 禁止做以下操作，除非用户明确要求：
  - 重命名/删除/批量改写上述文件或目录
  - 改变 Excel 文件结构（列/表名/格式）导致兼容性破坏
- `settings.ini` 为运行时生成：如需写入，建议使用“写临时文件 → 原子替换”的方式，避免崩溃造成半写文件。

### 5.4 图片管理与图片模式约束
- 图片管理窗口支持缩略图/列表视图；最近目录自动记录（最多 10）；收藏夹可手动增删；默认定位“此电脑”。
- 图片模式：默认按窗口等比最大化适配并居中显示；可选“记住图片缩放/平移”以沿用上次手动调整。
- 图片模式窗口不置顶，画笔工具条/点名窗口保持最前（除非用户手动隐藏）。
- 笔迹缓存仅内存保存“最终态”，不持久化；图片/PPT 页面背景不缓存。

## 6. 错误处理（产品级行为约束）
- 关键路径（点名/计时/画笔/演示控制/读写 Excel/互操作调用）应避免直接崩溃：
  - 使用局部 `try/catch` 包裹“外部依赖/不可信边界”（I/O、COM/Automation、PInvoke、设备/音频等）
  - 关键失败：**MessageBox** 告知用户可执行的下一步（例如：检查文件是否被占用、是否打开 PPT、是否启用权限）
  - MessageBox 文案简短明确，优先中文，必要时保留关键英文术语
- 外部集成降级策略：
  - Excel 失败：允许只读/或提示使用默认名单（如有）
  - 语音/播报失败：静默降级或提示（以“不阻塞课堂”为优先）

## 7. 调试与定位（高频问题速查）
### 7.1 焦点/窗口/鼠标穿透
- 使用 Spy++ / Inspect 工具核对：
  - `GetForegroundWindow()` 返回值与预期是否一致
  - 窗口样式：`WS_EX_NOACTIVATE` / `WS_EX_TRANSPARENT` 是否导致输入异常
- 对于 WPF/WinForms 互操作输入问题，优先在 interop 边界定位消息流转、按键（尤其是 Alt/系统键）行为。

### 7.2 演示控制（PowerPoint/WPS）
- 启用诊断日志：检查 `PresentationClassifier` 分类结果与输入发送（键盘/消息/自动化）是否匹配当前演示应用。
- 优先保障“不误触发、不乱翻页”，宁可降级为提示用户手动操作。

### 7.3 性能（画笔/绘制）
- 绘制优先使用 WriteableBitmap 等高效路径（以现有实现为准）。
- 高频事件（Move/Stroke）避免：
  - 阻塞 I/O
  - 大对象分配
  - 频繁创建 Brush/Pen（可冻结则冻结）

## 8. 输出要求（每次提交结果）
- 计划（checklist，简短）
- 修改点（按文件列出，说明“为什么这样改”）
- 验证（跑了哪些命令，结果如何）
- 风险/后续（如：尚未覆盖的边界条件、建议补测试点）
