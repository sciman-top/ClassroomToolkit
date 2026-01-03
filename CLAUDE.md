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
