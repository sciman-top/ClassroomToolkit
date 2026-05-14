# sciman Classroom Toolkit

中文 | [English](./README.en.md)

> 面向 Windows 教室电脑的课堂工具箱，覆盖随机点名、计时、屏幕批注、图片/PDF 讲解，以及 PowerPoint/WPS 放映控制。

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

## 覆盖范围

- 随机点名与课堂互动
- 倒计时、正计时和课堂活动计时
- 支持触屏、手写屏、数位板和鼠标的屏幕批注
- 图片与 PDF 全屏讲解、翻页、缩放和平移
- PowerPoint / WPS 放映导航与叠加批注
- 悬浮启动器，用于课堂中快速切换工具

## 非目标

本仓库不覆盖：

- 教务管理、成绩、作业或班级行政流程
- 强制云账号、服务端同步或在线协作
- 破坏 `students.xlsx`、`student_photos/`、`settings.ini` 的格式兼容
- Windows 桌面环境之外的跨平台运行

## 运行要求

- Windows 10 或 Windows 11
- 普通课堂使用建议下载打包发布版
- 开发需要 `.NET 10 SDK`
- 可选硬件：触屏一体机、手写屏、数位板、翻页笔、投影仪或外接显示器

## 快速开始

### 教师使用

1. 从 GitHub Releases 下载发布包。
2. 解压到固定目录，运行 `sciman Classroom Toolkit.exe`。
3. 确认悬浮启动器出现，再检查点名、图片/PDF 查看和 PPT/WPS 批注。

教师日常操作请优先看 [使用指南](./使用指南.md)。

### 开发者使用

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

## 本地数据

应用主要读取三类本地资源：

- `students.xlsx`：学生名册工作簿
- `student_photos/`：学生照片目录
- `settings.ini`：本地兼容设置文件

推荐照片结构：

```text
student_photos/
├── 1班/
│   ├── 001.jpg
│   └── 002.png
└── 2班/
    └── 101.jpg
```

数据约定：

- `students.xlsx` 中每个工作表对应一个班级
- `student_photos/` 按班级分文件夹
- 照片文件名优先使用学号
- 支持 `.jpg`、`.jpeg`、`.png`、`.bmp`
- 找不到学生数据时，应用可以生成模板
- 修改数据格式前必须考虑旧课堂电脑和已有文件的兼容性

## 仓库结构

```text
src/ClassroomToolkit.App          WPF UI、启动流程、窗口与课堂会话编排
src/ClassroomToolkit.Application  应用用例与跨模块协调
src/ClassroomToolkit.Domain       核心规则与业务模型
src/ClassroomToolkit.Services     运行时桥接与应用服务
src/ClassroomToolkit.Infra        配置、持久化与文件系统细节
src/ClassroomToolkit.Interop      Win32 / COM / WPS 集成边界
tests/ClassroomToolkit.Tests      自动化测试
docs/                            架构、计划、验证、证据与运行手册
```

## 构建与验证

固定交付门禁顺序为 `build -> test -> contract/invariant -> hotspot`：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

本仓也提供聚合质量门禁：

```powershell
powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

文档或注释类改动至少执行：

```powershell
git diff --check
```

## 文档入口

- [English README](./README.en.md)
- [教师使用指南](./使用指南.md)
- [文档目录](./docs/README.md)
- [架构文档](./docs/architecture/)
- [发布检查清单](./docs/runbooks/release-checklist.md)
- [课堂试点验收手册](./docs/runbooks/classroom-pilot-validation-runbook.md)

## 已知限制

- 主要目标是 Windows 教室电脑和触屏一体机
- 多显示器、DPI 缩放、投影、PPT/WPS 放映仍需要现场验证
- 缺少运行时、权限或设备驱动时，可能需要学校信息老师介入
- 学生名册、照片和设置均为本地文件，应由学校或使用者做好备份

## License

MIT
