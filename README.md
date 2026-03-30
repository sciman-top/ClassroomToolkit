# ClassroomToolkit

> 面向课堂教学场景的 Windows 桌面辅助工具集，聚焦点名、计时、批注和演示控制。

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

ClassroomToolkit 不是一个“大而全”的教学平台。它更接近一组面向真实课堂的本地工具：老师打开就能用，尽量减少切换窗口、临场找功能和重新配置的时间。

适合以下场景：

- 随机点名和课堂互动
- 倒计时、正计时和活动提醒
- PPT / WPS 放映时的批注和翻页
- 图片、试卷、讲义和 PDF 的投屏讲解
- 使用触控屏、手写板或翻页笔的课堂演示

## 核心能力

| 模块 | 主要能力 | 典型用途 |
|------|------|------|
| 点名 / 计时 | 随机点名、姓名照片展示、语音播报、倒计时 / 正计时、远程按键触发 | 课堂提问、限时活动、分组展示 |
| 画笔 / 白板 | 屏幕批注、普通笔 / 毛笔 / 橡皮、颜色与粗细设置、笔迹保存与回放 | 讲题板书、随堂批改、重点圈画 |
| 图片 / PDF | 全屏展示、翻页浏览、缩放平移、叠加批注 | 试卷讲解、图片赏析、PDF 讲义 |
| PPT / WPS | 自动识别放映状态、翻页控制、滚轮映射、放映层批注 | 幻灯片讲解、演示授课 |
| 启动器 | 悬浮入口、快速打开各窗口 | 上课中快速切换工具 |

## 项目边界

ClassroomToolkit 当前聚焦单机、本地、课堂即时使用，不覆盖下面这些方向：

- 不提供教务管理、成绩管理、作业分发等平台能力
- 不默认依赖云端账号、联网同步或服务器部署
- 不修改你的原始学生数据结构和文件格式
- 不承诺跨平台，当前只面向 Windows 桌面环境

这有意保持了产品边界，让项目优先解决“老师在课堂上当下要用”的问题。

## 系统要求

- Windows 10 或 Windows 11
- 普通使用者建议直接使用发布包
- 从源码运行建议安装 [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 可选设备：触控屏、手写板、翻页笔

## 下载与启动

### 普通使用者

推荐从 GitHub Releases 下载发布包，解压后直接运行 `ClassroomToolkit.exe`。

### 发布形态（当前）

- GitHub 版、标准版、离线版会分别生成独立目录，不混在同一个输出目录里。
- GitHub 版：源码与文档仓库，不面向直接课堂安装。
- 标准版：不内置 `.NET Desktop Runtime 10 x64`，体积更小，适合已安装运行时的机器。
- 离线版：内置 `.NET Desktop Runtime 10 x64` 安装包，目录中会额外带 `prereq/`，适合离线分发或受限网络环境。

第一次启动建议按这个顺序确认：

1. 悬浮启动器是否正常出现
2. 点名窗口能否读取班级和学生
3. 是否能打开一张图片或一个 PDF
4. 是否能在 PPT / WPS 放映时正常批注和翻页

### 开发者

如果你要从源码运行，请在仓库根目录执行：

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

如果你要发布到 GitHub，请将 README 中的仓库地址、Releases 地址和 Issue 地址替换为你的实际仓库链接。

## 课堂数据准备

程序会读取两类本地数据：

- `students.xlsx`：学生名册
- `student_photos/`：学生照片目录

建议目录结构如下：

```text
student_photos/
├── 1班/
│   ├── 001.jpg
│   └── 002.jpg
└── 2班/
    └── 101.png
```

数据约定：

- `students.xlsx` 以工作表区分班级，工作表名就是班级名
- 照片目录以班级分文件夹，文件名建议使用学号
- 支持 `.jpg`、`.jpeg`、`.png`、`.bmp`
- 首次启动找不到学生数据时，程序会自动生成模板

更细的课堂操作方式见 [教师使用指南](./使用指南.md)。

## 为什么适合课堂

- 本地运行，不依赖额外服务
- 入口集中，减少上课时切换窗口
- 针对 PPT / WPS、图片、PDF 和批注做了组合设计
- 保留传统课堂电脑环境的兼容路径，便于学校部署

## 已知限制与注意事项

- 主要面向 Windows 教室电脑，其他平台不在当前支持范围内
- 与演示软件、显示器缩放、多屏、高 DPI 相关的问题仍需现场验收
- 如果学校电脑缺少运行时、权限或驱动支持，通常需要信息老师协助处理
- 学生名册、照片和配置默认保留在本地，请自行做好备份和隐私管理

## 文档入口

- [教师使用指南](./使用指南.md)
- [架构文档](./docs/architecture/)
- [发布前检查清单](./docs/runbooks/release-prevention-checklist.md)
- [课堂试点验证 Runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## 开发与验证

常用命令：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
```

仓库结构：

```text
src/ClassroomToolkit.App          WPF 界面、启动入口、窗口与会话编排
src/ClassroomToolkit.Application  应用层用例与跨模块流程协调
src/ClassroomToolkit.Domain       业务规则与模型
src/ClassroomToolkit.Services     应用服务桥接与运行时能力
src/ClassroomToolkit.Infra        配置、持久化与文件系统细节
src/ClassroomToolkit.Interop      Win32 / COM / WPS 等高风险边界封装
tests/ClassroomToolkit.Tests      自动化测试
```

## 反馈与贡献

- 提交 Issue：请使用你的实际 GitHub 仓库 Issue 页面
- 提交 Pull Request 前请确保构建和测试通过
- 贡献前建议阅读仓库中的 `AGENTS.md`、`CLAUDE.md` 与相关架构文档

## License

本项目采用 [MIT License](./LICENSE)。
