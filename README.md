# ClassroomToolkit 课堂工具箱

<p align="center">
  <img src="./icon.ico" width="88" height="88" alt="ClassroomToolkit Logo" />
</p>

<p align="center">
  <strong>专为教室电脑、触屏一体机与电子黑板打造的免费开源轻量教学助手</strong>
</p>

<p align="center">
  <a href="./README.en.md">English</a> ·
  <a href="./使用指南.md">教师使用指南</a> ·
  <a href="https://github.com/sciman-top/ClassroomToolkit/releases/latest">最新版本下载</a> ·
  <a href="#快速上手">快速上手</a> ·
  <a href="#交流与作者">交流反馈</a>
</p>

<p align="center">
  <a href="https://github.com/sciman-top/ClassroomToolkit/releases/latest"><img src="https://img.shields.io/github/v/release/sciman-top/ClassroomToolkit?color=00B4D8&label=Release" alt="Release" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" /></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?logo=windows&logoColor=white" alt="Platform" /></a>
  <img src="https://img.shields.io/badge/Privacy-100%25%20Offline-2A9D8F?logo=safari&logoColor=white" alt="100% Offline" />
  <img src="https://img.shields.io/badge/Crash%20Safe-Fail--Closed-E76F51" alt="Crash Safe" />
  <a href="./LICENSE"><img src="https://img.shields.io/badge/License-MIT-457B9D" alt="License" /></a>
</p>

---

## 🌟 为什么选择 ClassroomToolkit？

很多老师在课堂上都经历过这些痛苦瞬间：
- 商业教学全家桶（如希沃白板、鸿合等）**体积庞大、启动缓慢**，教室的老旧 OPS 电脑动辄卡死转圈；
- 软件**强制绑定云账号、频繁弹窗更新**，甚至推送广告，学生名单传到外部云端更有**隐私泄露风险**；
- 网上下载的独立点名或计时器小工具，**一旦 PPT 全屏放映就被遮挡卡死**，且无法在幻灯片上自由批注翻页；
- 讲评试卷或课件难题时，想要在白板上详细推导，必须在多个软件之间繁琐切换窗口。

**ClassroomToolkit 由一线初中物理教师倾心设计打磨**，不追求臃肿庞杂的非教学功能，专注把课堂上最高频的几件事做到极致顺手、秒开秒用、绝对稳定：

| 对比维度 | ClassroomToolkit 课堂工具箱 | 传统商业教学软件 | 普通独立小工具 |
| :--- | :--- | :--- | :--- |
| **启动与资源** | **秒级启动**，极低内存与 CPU 占用，老旧电脑依然丝滑 | 启动慢（15~30s+），后台常驻，老电脑易卡死 | 参差不齐，容易内存泄露 |
| **隐私与网络** | **100% 纯本地离线**，零云端上传，免登录无账号 | 强制登录云账号，学生名册上传云端 | 部分需要联网注册或有后门 |
| **广告与费用** | **完全免费、开源无广告**，无任何弹窗推销 | 商业收费、增值付费、更新弹窗干扰 | 常常捆绑广告或流氓插件 |
| **PPT/WPS 放映** | **智能检测放映态**，画笔自动贴合，支持滚轮/按键翻页 | 经常抢占窗口焦点，导致幻灯片黑屏或失去控制 | 全屏放映时被彻底遮挡或闪烁 |
| **白板讲题** | **一键截取屏幕任意区域直入白板**，就地推导讲评 | 需先截图保存再手动导入白板，打断上课节奏 | 无此功能 |
| **崩溃防御** | **Win32/COM 异常强隔离与自动降级**，绝不卡死课堂 | 插件崩溃时往往连带 PPT 一起闪退 | 缺少异常处理，极易崩溃 |

---

## 🎯 8 大核心功能

### 1. 🎲 智能随机点名
- **班级与分组**：支持多班级自由切换，可按“全班”或指定“小组”精准抽选。
- **大字视觉与照片**：大屏投屏超大姓名自适应展示，后排学生一眼看清；支持配套显示学号与学生头像。
- **离线语音播报**：内置系统 TTS 语音合成，抽中后自动朗读学生姓名，调动课堂气氛。
- **无线翻页笔点名**：支持设置远程按键（默认 `Tab` 键），老师手持翻页笔在教室内走动即可远程一键触发点名。
- **防重复机制**：支持不重复抽取与一键重置，杜绝短时间内反复抽到同一位同学。

### 2. ⏱️ 课堂双模大字计时器
- **倒计时 & 正计时**：限时课堂小测、小组讨论用倒计时；学生演讲展示、活动记录用正计时。
- **巨幅醒目数字**：字体专为大屏与投影优化，远距离清晰易辨。
- **到时声音提醒**：倒计时结束自动播放提示音，节奏尽在掌握。
- **常用时长快选**：1 分钟、3 分钟、5 分钟快捷预设，课上随时一键开跑。

### 3. ✏️ 极速屏幕批注与电子白板
- **全屏透明涂鸦层**：无缝覆盖在桌面、浏览器、试卷、课件之上，随时圈画重点。
- **丰富笔触引擎**：
  - **毛笔 / 书法笔**：模拟真实书法顿笔与墨韵，板书笔迹苍劲优美；
  - **硬笔 / 普通笔**：日常快速勾画，笔迹跟手无延迟；
  - **荧光笔**：半透明重点着重高亮。
- **标准几何形状**：直线、虚线、箭头、虚线箭头、矩形（空心/实心）、椭圆、三角形一应俱全。
- **3 组快捷调色盘**：单击立即选用，再点/长按自由切换 12 种教学常用色彩与 3 档粗细。
- **完善的编辑操作**：笔迹撤销、局部橡皮、区域矩形擦除、一键清屏与截图保存。

### 4. ✂️ 屏幕选区直入白板（习题精讲神器）
- 讲解试卷、网页、题库或课件时，遇到重难点题目，点击“截图入白板”；
- 鼠标或触控笔在屏幕上框选题目区域，该区域**瞬间置入全屏白板**；
- 支持**经典黑板绿、纯白板、深灰板**等多种底色，老师可在题目旁留白处从容进行公式推导与步骤板书。

### 5. 🖼️ 高清图片与原生 PDF 课件讲解
- **Windows 原生渲染**：采用 DirectRenderer 引擎，无需引入几十兆的臃肿第三方 PDF 库，极速秒开大尺寸课件与试卷。
- **直觉交互**：滚轮平滑翻页、两指捏合自如缩放、单指拖拽平移，原位批注与圈点。
- **缩略图管理面板**：一键展开目录缩略图，几十页试卷与课件自由跳页。

### 6. 📽️ PowerPoint 与 WPS 放映深度协同
- **底层智能识别**：无需安装 PPT 插件，底层自动监听 Microsoft PowerPoint 与 WPS 演示文稿的放映状态。
- **无感贴合**：放映开始时批注层自动精准贴合放映窗口；放映窗口失去焦点切回后自动恢复贴合。
- **双向翻页联动**：在批注层使用鼠标滚轮或翻页键，可直接驱动 PPT/WPS 幻灯片前进与后退，上课再也不用在“画笔”和“切页”之间来回折腾。

### 7. 🎈 贴边极简悬浮球（Launcher Bubble）
- 启动后自动停靠在屏幕边缘，轻巧精致，**绝不遮挡课件主视觉**。
- 单击展开常用功能卡片，双击或点击空白处快速收起。
- 专为大屏触控设计，命中区域 ≥ 44px，手指轻轻一点即可唤出全部工具。

### 8. 🎨 教师友好视觉设计（Design System 2.0）
- 提供三套量身打造的高对比度护眼主题：
  - **Midnight Teal（午夜深青）**：深石墨配青绿主色，投影高对比度，长时间讲课不刺眼（默认推荐）；
  - **Blackboard（黑板护眼）**：还原经典墨绿黑板质感，暗光教室与电子黑板首选；
  - **Light（明亮专业）**：适合白天明亮办公室与备课使用。
- 严格遵循 4.5:1 无障碍对比度标准，远离劣质高光刺眼配色。

---

## 🚀 快速上手：3 分钟准备 <a id="快速上手"></a>

无需专业技术背景，普通老师只要 3 步即可轻松备好自己的专属课堂工具：

### 第一步：选择并下载适合你的版本

前往 [GitHub Releases](https://github.com/sciman-top/ClassroomToolkit/releases/latest) 下载：

| 交付版本 | 适用场景 | 说明 |
| :--- | :--- | :--- |
| **标准安装版 (`Setup.exe`)** | 普通联网教室电脑 | 文件体积最小，首次运行时自动检测并按需配置 .NET 运行时，支持静默自动升级 |
| **离线自包含安装版 (`Setup.exe`)** | 校园局域网断网环境、批量装机 | 自带全部运行环境，无网状态下一键装好即可使用 |
| **绿色便携版 (`.portable.zip`)** | U 盘随身携带、学校电脑权限受限 | 免安装，解压到 U 盘或桌面，双击 `启动.bat` 立即运行，数据全存在当前目录 |

### 第二步：准备学生名册（`students.xlsx`）

在数据文件夹中找到或新建 `students.xlsx`：
- **一个工作表代表一个班级**（工作表名称就是班级名，如 `初三1班`、`八年级2班`）；
- 第一行为表头，包含：`学号`、`姓名`、`分组`（分组可选）；
- 示例：

```text
students.xlsx
├── 初三1班
│   学号    姓名    分组
│   01     张晓明   第1组
│   02     李雨桐   第2组
└── 初三2班
    学号    姓名    分组
    01     王梓轩   第1组
```

*注：绿色便携版已内置规范的范例名册，直接修改成自己班的学生即可。*

### 第三步：放置学生照片（可选）

如果希望点名时同步弹出学生头像，按班级分文件夹存放照片：
```text
student_photos/
├── 初三1班/
│   ├── 01.jpg
│   └── 02.png
└── 初三2班/
    └── 01.jpg
```
- 照片文件名使用学生的**学号**即可（支持 `.jpg` / `.jpeg` / `.png` / `.bmp`）。
- 没有照片也能正常点名，点名界面会自动优雅隐藏照片卡片。

---

## ⌨️ 常用快捷操作速查

| 场景 | 按键 / 手势 | 对应动作 |
| :--- | :--- | :--- |
| **点名窗口** | `空格键 (Space)` | 触发 / 停止单人随机点名 |
| **点名窗口** | `Tab 键`（支持自定义） | **无线翻页笔远程点名** |
| **画笔批注** | `Esc 键` | 快速退出画笔模式 |
| **画笔工具条** | 单击调色球 | 切换当前画笔颜色 |
| **画笔工具条** | 长按调色球 / 图形按钮 | 唤出高级调色盘与图形选择菜单 |
| **图片 / PDF 讲解** | 鼠标滚轮 | 上一页 / 下一页平滑翻页 |
| **图片 / PDF 讲解** | 两指捏合 / 鼠标拖拽 | 自由缩放 / 平移局部细节 |
| **PPT/WPS 放映** | 鼠标滚轮 | 在画笔批注状态下直接控制幻灯片翻页 |

> 完整使用技巧与常见问题排查，请参阅 📖 [教师使用指南](./使用指南.md)。

---

## 🛡️ 数据安全与运行可靠性保障

- **Fail-Closed 故障闭环**：如果学生表格或配置文件在异常断电等意外中损坏，程序**绝不会用空白模板覆盖损坏的原文件**，并在格式迁移前自动创建带时间戳的完整备份；
- **异常强隔离**：Win32/COM/UIAutomation/WPS 集成均运行在有界隔离环境中，外部依赖故障时自动降级为标准模式，保证课堂教学不中断；
- **纯本地数据隔离**：所有学生信息、课堂笔记、板书截图与软件配置均储存在教师受控的本地存储中。

---

## 💻 开发者与技术栈

ClassroomToolkit 基于现代 **.NET 10** 与 **WPF** 开发，遵循整洁架构与模块化单体设计原则：

```text
src/ClassroomToolkit.App          WPF 组合根、界面视图、窗口拓扑与生命周期
src/ClassroomToolkit.Application  应用层用例编排、端口定义与跨领域调度
src/ClassroomToolkit.Domain       领域实体、点名算法、数据契约与不变量
src/ClassroomToolkit.Services     运行时桥接、输入法适配、TTS 语音引擎
src/ClassroomToolkit.Infra        工作簿编解码、配置持久化、SQLite 适配器
src/ClassroomToolkit.Interop      Win32 / COM / WPS 演示状态分类与故障降级
tests/ClassroomToolkit.Tests      3000+ 自动化回归测试与架构依赖契约
```

### 本地编译与运行

环境要求：Windows 10 22H2+ / Windows 11，[.NET 10 SDK](https://dotnet.microsoft.com/)。

```powershell
# 克隆仓库
git clone https://github.com/sciman-top/ClassroomToolkit.git
cd ClassroomToolkit

# 还原并编译
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug

# 运行主程序
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

### 质量门禁与验证

本项目执行严格的质量把控体系（`build -> test -> contract/invariant -> hotspot`）：

```powershell
# 运行标准本地质量门禁（涵盖编译、契约测试与热点行预算审计）
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

更多开发规范、设计决策与发布流程详见：
- [项目架构文档](./docs/architecture/README.md)
- [项目状态与主线快照](./docs/project-status.md)
- [发布前检查清单](./docs/runbooks/release-checklist.md)
- [代码贡献规范](./CONTRIBUTING.md) · [支持渠道](./SUPPORT.md)

---

## 🤝 交流与作者 <a id="交流与作者"></a>

ClassroomToolkit 由一线初中物理教师**广州番禺王耀强**业余倾力打造，致力于用现代工程技术为一线讲台提供最简单、最好用、最可靠的数字化工具。

- **微信公众号**：`sciman逸居`（分享教师数字化教学思考与实用工具）
- **教师教研交流 QQ 群**：`323728546`（初中物理教研与数字化教学工具交流群）
- **知乎专栏**：[sciman 的知乎专栏](https://www.zhihu.com/people/sciman/columns)
- **问题反馈与建议**：欢迎在 [GitHub Issues](https://github.com/sciman-top/ClassroomToolkit/issues) 提交 Bug 报告或功能心愿单！

如果你觉得 ClassroomToolkit 对你的课堂教学或学校有所帮助，欢迎给本项目点一个 ⭐️ **Star**，并分享给身边的教师同行！

---

## 📄 开源许可证

本项目基于 [MIT 许可证](./LICENSE) 开源，允许自由使用、修改与分享。
