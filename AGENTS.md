# Repository Guidelines

# Output Language Policy

## 用户可见输出语言
- 所有最终输出、分析说明、代码解释、设计决策、文档、列表、任务进度更新**必须使用中文**
- 技术术语保持英文原文（如 WPF, Grid, DependencyProperty），避免歧义
- 允许在代码注释中使用中文

## 模型内部推理（不可见内容）
- 模型的内部思考、推理链、规划语句（chain-of-thought / reasoning）无需翻译成中文
- 若 reasoning 被展示，则无需更改英文形式
- **模型在开始执行前必须用中文复述任务理解**（用于同步上下文）

## 上下文连续性
每次输出前执行以下步骤：
1. 用中文总结当前任务理解
2. 输出结果（中文）
3. 若 reasoning 被展示，无需翻译，保持英文

## Project Structure & Module Organization
- `src/`: 主体代码，WPF 桌面应用与各层项目。
  - `src/ClassroomToolkit.App/`: WPF UI、XAML、画笔与窗口逻辑。
  - `src/ClassroomToolkit.Domain/`: 领域模型与核心业务逻辑。
  - `src/ClassroomToolkit.Services/`: 应用服务层。
  - `src/ClassroomToolkit.Infra/`: 持久化与配置读取。
  - `src/ClassroomToolkit.Interop/`: Win32/WPS 交互等互操作代码。
- `tests/`: 单元测试项目（xUnit）。
- `student_photos/`、`students.xlsx`、`icon.ico`: 运行时资源与示例数据。

## Build, Test, and Development Commands
- `dotnet build ClassroomToolkit.sln`：构建全部项目。
- `dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`：启动桌面应用。
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`：运行单元测试。

## Coding Style & Naming Conventions
- 缩进 4 空格；保持现有 C# 风格。
- 命名：类/方法 `PascalCase`，字段/局部变量 `camelCase`，常量 `UPPER_SNAKE_CASE`。
- 当前未配置格式化工具或 lint；改动需与现有代码风格一致。

## Testing Guidelines
- 测试框架：xUnit + FluentAssertions（可选 coverlet 收集覆盖率）。
- 测试位置：`tests/ClassroomToolkit.Tests/`。
- 运行：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`。

## Commit & Pull Request Guidelines
- 当前仓库无既定提交规范，建议使用清晰的祈使式动词，例如：`Fix ink flow calculation`。
- PR 说明需包含：改动目的、复现/验证步骤；涉及 UI 变更请提供截图或 GIF。

## Configuration & Data Notes
- `students.xlsx` 和 `student_photos/` 为持久数据路径，避免破坏性修改。
- 如需诊断启动问题，可关注运行日志与相关设置项。
