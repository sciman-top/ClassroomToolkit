# sciman Classroom Toolkit

中文 | [English](./README.en.md)

> 面向 Windows 教室电脑的课堂工具箱，覆盖随机点名、计时、屏幕批注、图片/PDF 讲解，以及 PowerPoint/WPS 放映控制。

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

## 项目定位

本仓库的目标是让教师在一台普通 Windows 教室电脑上，用一个本地应用完成课堂里最常见的几类动作：

- 随机点名与课堂互动
- 倒计时、正计时和活动计时
- 支持触屏、手写屏、数位板和鼠标的屏幕批注
- 图片与 PDF 全屏讲解、翻页、缩放和平移
- PowerPoint / WPS 放映导航与叠加批注
- 悬浮启动器，用于课堂中快速切换工具

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

## 最新状态（2026-08-17）

最近几批工作集中在课堂高频链路的稳定性与触控体验：

- 图片 / PDF 全屏下，白板按钮会先给出“截图入白板 / 纯白板 / 底色白板”入口，而不是直接跳进白板。
- 3 个快捷画笔现在支持各自独立的粗细；再次点击同一快捷画笔，会弹出颜色和 3 档粗细选择。
- 图片 / PDF 批注的撤销链路已补到运行态历史、缓存和持久化状态，避免“撤销后移动一下又消失”。
- 悬浮学生照片、工具条、点名窗口、启动器之间的窗口层级做了首帧与复显加固，尽量避免课堂中首帧遮挡和抢焦点。
- 画笔设置对话框补了构造期空引用防护，避免设置按钮在 XAML 初始化阶段偶发崩溃。
- JSON 设置现在把 schema 损坏、文件锁和短暂 IO 失败统一视为不可安全覆盖；只有显式成功重载后才恢复保存，未知 section/key 会继续保留。
- 损坏的 `students.xlsx` 不再被示例模板覆盖，后续状态保存也会被阻断；合法旧格式规范化前会生成按 SHA-256 去重的原字节备份。
- PDF 渲染已从 `PdfiumViewer.Core 1.0.4` 和 2018 native PDFium 迁移到 Windows 原生 `Windows.Data.Pdf`；损坏文件降级、大页面内存预算、128 页元数据、黑白视觉内容及 96/144 DPI 像素尺寸均有自动化保护。
- 共享原子写入不再降级为 `File.Copy(overwrite: true)`；不支持 `File.Replace` 时改用同目录覆盖移动，并移除了只有一个调用者的 fallback policy。
- 墨迹诊断文本和工作簿原子写的实现字符串断言已退役；损坏读取、锁文件、WAL 恢复与临时文件清理由现有行为测试继续保护。
- WPS hook 的停止/释放、拦截门禁与订阅者异常隔离已改用可控后台队列的行为测试，4 个源码字符串断言退役。
- 旧版 INI 只在真正持久化迁移前生成按内容哈希去重的备份；只读加载不再制造重复备份，备份失败会阻止覆盖。
- 4 个单表达式 Paint policy 及其逐字复述测试已内联删除；墙钟画笔微基准改为 full/focused 运行，standard 不再受机器争用误报影响。
- 依赖闭包删除未使用的 SourceGear native SQLite 与测试重复固定，.NET 10 包更新到 10.0.11，Test SDK 更新到 18.9.0。

本轮收口后的本地验证快照：

- `dotnet build ClassroomToolkit.sln -c Release`：通过，0 warning / 0 error
- full Release stable tests（排除核心契约，包含 9 个性能预算）：通过，3009/3009
- contract / invariant：通过，29/29
- `latest-all` analyzer：0 diagnostics；依赖漏洞：0
- 当前代码阻断项：无
  - 学生工作簿和 JSON 设置读取失败均 fail-closed，原文件不会被模板或默认值覆盖
  - PDF 渲染不再携带第三方 native 引擎；仓库验证不外推为真实课件、DPI、投影或课堂视觉验收

更多背景请看：

- [文档目录](./docs/README.md)
- [当前接手说明](./docs/handover.md)
- [高风险变更证据](./docs/change-evidence/)

## 快速开始

### 教师使用

1. 从 GitHub Releases 下载 `ClassroomToolkit-*-Setup.exe` 安装包。
2. 普通联网教室选择 `standard`；校园内网隔离、批量装机或缺少运行时的电脑选择 `offline`；临时设备、U 盘或不希望安装的电脑选择 `portable` 绿色便携版。
3. 安装后确认悬浮启动器出现，再检查点名、图片 / PDF 查看、白板入口和 PPT / WPS 批注。

两类安装包功能相同：`standard` 为 framework-dependent 安装版，首次安装会按需安装 .NET Desktop Runtime；`offline` 为 self-contained 安装版。它们通过独立更新通道下载更新，已下载更新会在下次启动时应用。`portable` 为 self-contained 绿色版，解压后运行根目录 `启动.bat`，数据保存在同级 `data/`，启动时只检查 GitHub 正式版本并在发现更新时打开下载页，不自动替换文件。公开源码以同一版本的 `ClassroomToolkit-Source-<版本号>.zip` 单独提供，不进入教师安装目录。

教师日常操作请优先看 [使用指南](./使用指南.md)。

### 开发者使用

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

如果要准备发布包，优先使用仓库内脚本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Release -Profile full
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/prepare-release-artifacts.ps1 -Version <版本号> -PackageMode all -Configuration Release -EnsureLatestRuntime
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
- `students.xlsx` 不存在时，应用可以生成模板；文件已存在但损坏时会保留原件并提示读取失败
- 修改数据格式前必须考虑旧课堂电脑和已有文件的兼容性
- 开发态继续使用解决方案根目录的数据；已安装版本将课堂数据保存在 `%LOCALAPPDATA%\ClassroomToolkit\data`，首次启动时仅在目标不存在时复制旧安装目录中的名册和照片，避免自动更新覆盖数据。

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

本仓也提供聚合质量门禁：

```powershell
powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

普通改动优先运行受影响测试与 build；`quick` 只做快速反馈，`standard` 用于共享或高风险 seam 的阶段收口并排除墙钟性能微基准。画笔性能改动应聚焦运行 `BrushPerformanceGuardTests`；发布前或依赖变化使用 `full`，统一纳入性能预算、漏洞、升级候选和 `latest-all` analyzer 审计。

文档或注释类改动至少执行：

```powershell
git diff --check
```

如果你是基于当前主分支继续开发，请先查看 [docs/handover.md](./docs/handover.md)；上述数量只代表本次精简后的仓库验证，发布仍需课堂现场验收。

## 文档入口

- [English README](./README.en.md)
- [教师使用指南](./使用指南.md)
- [文档目录](./docs/README.md)
- [当前接手说明](./docs/handover.md)
- [技术债与稳定性清单](./docs/tech-debt-backlog.md)
- [高风险变更证据](./docs/change-evidence/)
- [发布检查清单](./docs/runbooks/release-checklist.md)
- [课堂试点验收手册](./docs/runbooks/classroom-pilot-validation-runbook.md)

## 已知限制与发布边界

- 主要目标是 Windows 教室电脑和触屏一体机
- 多显示器、DPI 缩放、投影、真实课堂 PDF 视觉效果和 PPT / WPS 放映仍需要现场验证
- 缺少运行时、权限或设备驱动时，可能需要学校信息老师介入
- 学生名册、照片和设置均为本地文件，应由学校或使用者做好备份
- 当前代码门禁已恢复全绿；发布基线仍应补齐多显示器、DPI、投影、PPT / WPS 与学生照片悬浮层的现场验证

## License

MIT
