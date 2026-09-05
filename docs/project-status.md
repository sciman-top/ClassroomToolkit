# 项目状态快照（2026-08-29）

本页是面向开发者的唯一当前状态入口（承接原 handover 与 README 精简前的“最新状态”清单）；README 只保留概要并链接到这里。

## 当前主线

- 产品主链：启动 -> 名单/配置加载 -> 课堂操作 -> Interop 降级 -> 可观察结果与恢复。
- 代码侧没有已知 P0 阻断；发布仍需真实教室的多显示器、DPI、投影、PPT/WPS、触控和学生照片悬浮层验收。
- 历史重构任务图、自动循环和治理快照已退役；不要从 Git 历史恢复为日常门禁。

## 真值边界

- 代码、当前配置与 fresh 测试结果优先于历史文档；`repo_verified` 不等于课堂现场 `live_accepted`。
- PDF 自动化只证明当前 Windows 主机上的尺寸、损坏/大页面降级、基础黑白视觉与渲染预算；真实课件、密码/特殊字体、DPI、多屏和投影效果仍需现场验收。
- 普通变更由 Git diff 和测试输出留痕；只有数据迁移、Interop 生命周期、发布或不可逆修复才新增 change-evidence。

## 2026-08-29 全仓审计与减负轮

- 修复基线红测试：`AtomicFileReplaceUtility` 瞬时锁重试预算放宽到 5×50ms；`Thread.Sleep` 契约测试引入与阻塞等待契约同构的显式豁免清单（唯一豁免：原子替换的有界重试）。
- 健壮性：墨迹脏页协调器与 sidecar 持久化加文档级锁（后台自动保存与 UI 同步保存不再互覆盖丢页）；点名 ViewModel 释放不再提前销毁在途任务持有的取消令牌；settings.ini 解码改为“BOM → UTF-8 严格 → GB18030 → 宽松兜底”（旧版 GBK/ANSI 文件不再被读成乱码）；键盘/WPS 全局钩子在 unhook 失败时保留句柄可重试，且不再把 LL 钩子安装到无消息泵的线程池线程。
- 性能：照片模式进入/换页改为后台解码（与 PDF 路径同构）；每笔画的墨迹 WAL 落盘改为 400ms 防抖合并（页面持久化仍同步清条目）；点名照片悬浮层解码真正异步；退出时的墨迹孤儿清理移到后台；启动期去掉一次全可视树边框修复遍历；画笔换色不再累积临时 .cur 文件与 GDI 光标句柄。
- 删减：零引用的 Application 抽象（IStudentRepository/ISettingsStore/ITelemetrySink/IInkStorageGateway）、Presentation 网关三件套、IHandleValidator、ComObjectManager 及其源码形状测试、400 行无人引用的 RollCallSqliteStoreAdapter、SafeBorder/GlobalBorderFixer、两份重复的墨迹噪点瓷砖缓存（合并为 `InkNoiseTileCache`）、18 个零引用资源键、4 份重复的 NoopEffectRunner。
- 治理：退役 2026Q2 兼容矩阵工具链（5 个 validation 脚本、git-acl-guard、logging 阈值检查及配套 runbook/报告文档）；`ctoolkit.ps1` 移除指向不存在脚本的死参数与 `git add -A` 自动提交；`.dotnet-home/` 运行态缓存退出 git 跟踪；学生簿规范化备份改写入 `backups/` 子目录并滚动保留 10 份（存量已迁移）。
- 已核实无需修改：诊断探针的约 4 秒等待仅在后台线程执行；点集合更新均有 dispatcher 编组；全部关键写入（INI/JSON/xlsx/WAL）保持临时文件+原子替换。

## 2026-08-27 及之前

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
- 发布链已固定为标准安装版、离线安装版、绿色便携版和公开源码包四类交付物；绿色版只检查 GitHub 正式 Release 并打开下载页，不自动替换文件。
- 发布聚合入口使用 `.staging/<version>` 临时目录，成功后最终版本目录只保留安装器、绿色 ZIP、源码 ZIP 和 manifest；旧候选与历史验证输出归档到 `artifacts/archive/legacy-outputs/`。

本轮收口后的本地验证快照：

- `dotnet build ClassroomToolkit.sln -c Release`：通过，0 warning / 0 error
- full Release stable tests（排除核心契约，包含性能预算）：通过，3024/3024
- contract / invariant：通过，29/29
- `latest-all` analyzer：0 diagnostics；依赖漏洞：0
- 当前代码阻断项：无
  - 学生工作簿和 JSON 设置读取失败均 fail-closed，原文件不会被模板或默认值覆盖
  - PDF 渲染不再携带第三方 native 引擎；仓库验证不外推为真实课件、DPI、投影或课堂视觉验收

相关入口：[高风险变更证据](./change-evidence/)、[发布检查清单](./runbooks/release-checklist.md)、[技术债清单](./tech-debt-backlog.md)。
