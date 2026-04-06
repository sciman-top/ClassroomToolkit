# sciman Classroom Toolkit

中文说明 | [English](./README.en.md)

> 面向真实课堂的一组 Windows 本地教学工具，聚焦随机点名、计时、屏幕批注、图片/PDF 讲解，以及 PPT / WPS 放映控制。

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

sciman Classroom Toolkit 不是教务平台，也不追求“大而全”。它优先解决老师在课堂上会立刻遇到的问题：减少窗口切换、减少临场配置、让点名、计时、批注和演示控制尽量在一套本地工具里完成。

## 适用场景

- 随机点名、分组互动、课堂限时活动
- 使用触控屏、手写板或翻页笔进行课堂讲解
- 在 PPT / WPS 放映时直接批注、翻页
- 用图片、试卷、讲义、PDF 做投屏讲评
- 需要本地运行、弱联网或离线部署的教室环境

## 核心能力

| 模块 | 主要能力 | 典型用途 |
|------|------|------|
| 点名 / 计时 | 随机点名、学生照片展示、语音播报、倒计时 / 正计时、远程按键触发 | 课堂提问、限时活动、分组展示 |
| 画笔 / 白板 | 屏幕批注、普通笔 / 毛笔 / 橡皮、颜色与粗细设置、笔迹保存与回放 | 讲题板书、随堂批改、重点圈画 |
| 图片 / PDF | 全屏展示、翻页浏览、缩放平移、叠加批注、区域截图入白板 | 试卷讲解、图片赏析、PDF 讲义、临时截题讲评 |
| PPT / WPS | 自动识别放映状态、翻页控制、滚轮映射、放映层批注 | 幻灯片讲解、演示授课 |
| 启动器 | 悬浮入口、快速打开各窗口 | 上课中快速切换工具 |

## 项目边界

当前仓库明确不覆盖以下方向：

- 不提供教务管理、成绩管理、作业分发等平台能力
- 不依赖云端账号、服务器或联网同步才能使用
- 不改变 `students.xlsx`、`student_photos/`、`settings.ini` 的既有语义
- 不承诺跨平台，当前只面向 Windows 桌面环境

这让项目保持在“课堂即时可用”的边界内，避免功能堆积带来的部署和维护负担。

## 系统要求

- Windows 10 或 Windows 11
- 建议分辨率 1920x1080 及以上
- 普通使用者建议直接使用发布包
- 从源码运行建议安装 [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 可选设备：触控屏、手写板、翻页笔、外接显示器 / 投影

## 快速开始

### 普通使用者

推荐从 [GitHub Releases](https://github.com/sciman-top/ClassroomToolkit/releases) 下载发布包，解压后直接运行 `sciman Classroom Toolkit.exe`。

首次启动建议按这个顺序确认：

1. 悬浮启动器是否正常出现
2. 点名窗口能否读取班级和学生
3. 是否能打开一张图片或一个 PDF
4. 是否能在 PPT / WPS 放映时正常批注和翻页

### 开发者

在仓库根目录执行：

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

## 发布形态

- GitHub 版：源码与文档仓库，不直接面向教室安装
- 标准版：不内置 `.NET Desktop Runtime 10 x64`，体积更小
- 离线版：额外附带运行时安装包，适合离线分发或受限网络环境

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

更细的课堂操作说明见 [教师使用指南](./使用指南.md)。

## 文档入口

- [教师使用指南](./使用指南.md)
- [English README](./README.en.md)
- [架构文档](./docs/architecture/)
- [发布前检查清单](./docs/runbooks/release-prevention-checklist.md)
- [课堂试点验证 Runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## 开发与验证

常用命令：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

如果你要长期接管一个固定的浏览器，会话可以直接用仓库里的启动脚本：

```powershell
powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Name github -Url https://github.com/sciman-top/ClassroomToolkit
agent-browser --cdp 9222 open https://github.com/sciman-top/ClassroomToolkit
```

这个脚本会用独立 profile 拉起 Chrome/Edge，登录态会保留在本机的专用目录里，不会和日常浏览器混用。

完整说明见 [tools/browser-session/README.md](/E:/CODE/ClassroomToolkit/tools/browser-session/README.md)。

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

## 已知限制

- 主要面向 Windows 教室电脑，其他平台不在当前支持范围内
- 与演示软件、显示器缩放、多屏、高 DPI 相关的问题仍需现场验收
- 如果学校电脑缺少运行时、权限或驱动支持，通常需要信息老师协助处理
- 学生名册、照片和配置默认保留在本地，请自行做好备份和隐私管理

## 反馈与贡献

- 提交 Issue：<https://github.com/sciman-top/ClassroomToolkit/issues>
- 提交 Pull Request 前请先确保构建和测试通过
- 贡献前建议阅读仓库中的架构文档与治理说明

## License

本项目采用 [MIT License](./LICENSE)。
