# ClassroomToolkit 课堂工具箱

中文 | [English](./README.en.md)

> 面向 Windows 教室电脑的免费开源课堂工具箱：随机点名、计时、屏幕批注、图片/PDF 讲解、PowerPoint / WPS 放映控制，一个应用全搞定。

[![Release](https://img.shields.io/github/v/release/sciman-top/ClassroomToolkit)](https://github.com/sciman-top/ClassroomToolkit/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

## 这是什么

一款给中小学教师用的 Windows 桌面工具。它不追求大而全，只把课堂上最高频的几件事做顺手：

- 🎲 **随机点名** — 按班级、分组抽人，可显示学号和头像，支持语音播报和翻页笔远程点名
- ⏱️ **计时器** — 倒计时、正计时、课堂活动计时，时间到自动提示
- ✏️ **屏幕批注与白板** — 支持触屏一体机、手写屏、数位板和鼠标；可区域截图入白板讲评，笔迹可撤销、保存和回放
- 🖼️ **图片 / PDF 讲解** — 全屏展示、滚轮翻页、缩放平移，边讲边圈画；PDF 使用 Windows 原生渲染，无第三方 native 引擎
- 📽️ **PPT / WPS 放映控制** — 自动检测放映状态，直接在幻灯片上批注和翻页
- 🚀 **悬浮启动器** — 一个屏幕边缘小圆钮，课上快速切换所有工具

特点：

- **纯本地运行** — 学生名册（`students.xlsx`）、照片和设置都是本地文件，不强制云账号，不上传数据
- **数据安全** — 名册或设置文件损坏时 fail-closed，原文件不会被模板或默认值覆盖；格式迁移前自动备份
- **为老旧教室电脑设计** — 提供联网安装版、离线自包含安装版和免安装绿色版；外部设备或环境异常时可降级，不崩溃、不卡死课堂

不覆盖的范围：教务管理、成绩作业流程、在线协作，以及 Windows 之外的跨平台运行。

## 下载安装

从 [GitHub Releases](https://github.com/sciman-top/ClassroomToolkit/releases/latest) 下载，三类交付物功能相同，按教室环境选择：

| 交付物 | 适用场景 |
|--------|----------|
| `Setup.exe`（standard，framework-dependent） | 普通联网教室；首次安装按需安装 .NET Desktop Runtime |
| `Setup.exe`（offline，self-contained） | 校园内网隔离、批量装机或缺少运行时的电脑 |
| `ClassroomToolkit-<版本>-portable.zip` | 临时设备、U 盘或不希望安装的电脑；解压后运行 `启动.bat` |

安装版支持应用内更新（下载后在下次启动时应用）；绿色版只检查新版本并打开下载页，不自动替换文件。系统要求：Windows 10 / 11。

上手指引见[教师使用指南](./使用指南.md)——从课前 3 分钟检查清单到各功能的具体操作和常见问题排查都在里面。

## 数据放在哪里

- `students.xlsx`：学生名册，一个工作表对应一个班级（字段建议：学号、姓名、分组）
- `student_photos/`：学生照片，按班级分文件夹，文件名用学号，支持 `.jpg` / `.jpeg` / `.png` / `.bmp`
- 三种运行形态布局一致：数据统一放在运行根的 `data/` 目录下（开发态为解决方案根 `data/`，安装版为 `%LOCALAPPDATA%\ClassroomToolkit\data`，便携版为程序旁 `data/`）
- 旧布局（开发态根目录、旧安装目录）中的名册和照片会在首次运行时自动复制进 `data/`，原文件保留不动
- 绿色便携版的 `data/` 目录自带一份范例名册和照片目录说明，可直接改成自己的数据

## 面向开发者

技术栈：WPF (.NET 10)，分层为 App / Application / Domain / Services / Infra / Interop，3000+ 自动化测试与固定质量门禁（`build -> test -> contract/invariant -> hotspot`）。

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

详细门禁、发布打包脚本和仓库结构见下方文档入口。欢迎 issue 和 PR，见 [CONTRIBUTING](./CONTRIBUTING.md)。

## 文档入口

- [English README](./README.en.md)
- [教师使用指南](./使用指南.md)
- [文档目录](./docs/README.md)
- [项目状态快照](./docs/project-status.md)
- [当前接手说明](./docs/handover.md)
- [技术债与稳定性清单](./docs/tech-debt-backlog.md)
- [发布检查清单](./docs/runbooks/release-checklist.md)
- [安全策略](./SECURITY.md) · [贡献指南](./CONTRIBUTING.md)

## 仓库结构

```text
src/ClassroomToolkit.App          WPF UI、启动流程、窗口与课堂会话编排
src/ClassroomToolkit.Application  应用用例与跨模块协调
src/ClassroomToolkit.Domain       核心规则与业务模型
src/ClassroomToolkit.Services     运行时桥接与应用服务
src/ClassroomToolkit.Infra        配置、持久化与文件系统细节
src/ClassroomToolkit.Interop      Win32 / COM / WPS 集成边界
tests/ClassroomToolkit.Tests      自动化测试
scripts/                         质量门禁、验证、发布和环境脚本
docs/                            架构、验收、少量高风险证据与运行手册
```

## 构建与验证

固定交付门禁顺序为 `build -> test -> contract/invariant -> hotspot`：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate!=CoreContract&Gate!=Performance"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate=CoreContract"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

也可使用聚合门禁 `scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`。`quick` 只做快速反馈；`standard` 用于共享或高风险 seam 的阶段收口；发布前或依赖变化使用 `-Profile full`（含性能预算、漏洞与 `latest-all` analyzer 审计）。文档改动至少运行 `git diff --check`。

如果你是基于当前主分支继续开发，请先查看 [docs/handover.md](./docs/handover.md)；本地门禁通过不代表完成课堂现场验收。

## 已知限制与发布边界

- 主要目标是 Windows 教室电脑和触屏一体机
- 多显示器、DPI 缩放、投影、真实课堂 PDF 视觉效果和 PPT / WPS 放映仍建议现场验证
- 缺少运行时、权限或设备驱动时，可能需要学校信息老师介入
- 学生名册、照片和设置均为本地文件，应由学校或使用者做好备份

## License

MIT
