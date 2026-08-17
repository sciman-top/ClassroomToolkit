# 2026-08-17 深度审计收口证据

## 范围与结论

本切片审计正确性、UI 阻塞、异常/生命周期、配置迁移、性能探针、模块 seam、测试/门禁和依赖闭包。当前工程终态继续采用 `.NET 10 + WPF` 模块化单体；没有证据支持全量重写，后续按触及路径渐进深化模块。

## 已修复

- 墙钟画笔微基准在全套测试并发下误报：改为独占 xUnit collection，并标记 `Gate=Performance`；standard 排除，full/focused 保留。
- INI 迁移在每次加载生成重复备份且吞掉备份失败：迁移改为纯计算，Repository 只在保存旧版本前创建内容哈希备份；备份冲突/失败时 fail-closed，保存继续使用原子写。
- 4 个只有一个调用者的单表达式 Paint policy 与 12 个复述型测试用例退役，逻辑回到所属行为；保留有容差、状态机或真实 adapter seam 的模块。
- 删除未使用的 `SourceGear.sqlite3` native SQLite 实现和测试项目 7 个重复/未使用直接包引用；.NET 10 包更新到 10.0.11，Test SDK 更新到 18.9.0，lockfile 已刷新。
- 退役两份已失真的日期型架构台账，建立 `docs/architecture/README.md` 当前真值；不再把“每个分支拆独立 Policy”当作模块化要求。

## Fresh verification

- `dotnet restore ClassroomToolkit.sln --locked-mode -m:1`：PASS。
- `dotnet build ClassroomToolkit.sln -c Debug -m:1`：PASS，0 warning / 0 error。
- full stable tests：PASS，2997/2997；其中 8 个性能预算在独占 collection 中运行。
- `Gate=CoreContract`：PASS，29/29。
- hotspot：PASS，所有生产 C# 文件不超过 1200 行。
- dependency vulnerability：PASS，0 known vulnerable packages。
- dependency upgrade audit：PASS；仅 xUnit 4/MTP、传递 Bcl 和 SixLabors.Fonts major 项处于有效 waiver。
- `latest-all` analyzer：PASS，0 diagnostics。
- Release 设置加载探针：4.2 KiB hot P95 0.467 ms；512 KiB hot P95 1.737 ms；cold P95 分别 0.786 ms / 2.247 ms。

## 风险、回滚与真值边界

- 设置备份文件名改为 `*.bak-v2.0-<SHA256>.ini`；旧备份不删除且仍可人工恢复。回滚时同时回滚 Migrator、Repository 与两组行为测试。
- 依赖回滚必须同时回滚 `.csproj` 和 `packages.lock.json`，再运行 locked restore 与 full profile。
- 门禁语义回滚需同时回滚性能 Trait、stable-test filter、README 与项目契约。
- 当前没有最近 24 小时 UI 运行日志，因此图片解码、dispatcher 排队、墨迹重绘 P95 仍缺现场样本。多显示器、DPI、投影、PPT/WPS、触控和照片悬浮层仍需课堂 `live_accepted`。

## 2026-08-17 审计问题修复补充

### 已修复

- 学生工作簿读取失败不再返回可保存的示例模板；异常由应用用例转为可见错误并禁用持久化，同一 store 后续保存也会拒绝覆盖原文件。
- 合法旧格式工作簿规范化前创建 `*.bak-normalize-<SHA256>.xlsx`，并在写回前校验备份内容哈希；备份保留公式、样式和其他未建模内容的原始字节。
- JSON 设置任意既有文件读取失败都会保持覆盖阻断；保存前结构预检不能解除阻断，只有显式成功 `Load()` 或成功保存才可恢复。
- 零页 PDF 在所有权转移前立即释放，失败分支不再遗留 native document/file handle；`IPdfDocumentHost` 隔离当前 PDFium 实现。
- WPS hook 后台队列可注入，停止/释放后的已排队回调失效、拦截门禁和订阅者异常隔离由行为测试证明，4 个源码字符串断言退役。

### Fresh verification

- 红测复现：损坏工作簿原件/后续保存、规范化备份、JSON transient load、零页 PDF 所有权均在旧行为上失败。
- focused：`StudentWorkbookStoreTests` 7/7、JSON + workbook 23/23、PDF 生命周期 2/2、Interop 生命周期/派发 11/11，全部 PASS。
- standard：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`，build 0 warning / 0 error，普通测试 2991/2991，核心契约 29/29，hotspot PASS。
- 本切片未改变依赖或发布输入，按项目比例门禁不重复运行 full。

### 恢复与开放边界

- 工作簿规范化回滚时，优先从哈希匹配的 `.bak-normalize-*.xlsx` 恢复；若损坏读取已触发阻断，人工恢复或替换原件并成功重载后再保存。
- JSON 锁或短暂 IO 恢复后先显式重载，再允许保存；不得通过保存前预检绕过失败状态。
- PDF 生命周期改动回滚需同时回滚 `IPdfDocumentHost`、窗口字段/打开逻辑和两项所有权测试。
- 该阶段的 PDFium 供应链风险已由下述渲染器迁移切片关闭；多显示器、DPI、投影、PPT/WPS、触控及真实 PDF 视觉仍需课堂 `live_accepted`。

## 2026-08-17 PDF 渲染器迁移

### 决策与许可证边界

- 选择 Windows 10/11 随系统提供的 [`Windows.Data.Pdf.PdfDocument`](https://learn.microsoft.com/uwp/api/windows.data.pdf.pdfdocument) 与 [`PdfPage.RenderToStreamAsync`](https://learn.microsoft.com/uwp/api/windows.data.pdf.pdfpage.rendertostreamasync)，App/Tests 目标框架提升到 `net10.0-windows10.0.19041.0`；当前产品基线仍是 Windows 10 22H2+ / Windows 11 22H2+。
- 删除 `PdfiumViewer.Core 1.0.4` 和 `PdfiumViewer.Native.x86_64.no_v8-no_xfa 2018.4.8.256`，App/Test lockfile 与新目标框架输出均不再包含 `PdfiumViewer` 或 `pdfium.dll`。
- 启动兼容探针不再把归档的 `pdfium.dll` 当成发布必需文件；行为测试保证迁移后的正常发布包不会产生 `native-pdfium-missing` 误报。
- 系统 API 不新增第三方渲染器包、native redistributable 或对应开源许可证条目，部署继续受 Windows 平台许可约束。MIT 的 `PDFtoImage 5.4.0` 仅作为失败回退候选评估，因会重新引入 SkiaSharp/PDFium native 闭包而未采纳。

### 实现与兼容保护

- 保留现有一基页码 `IPdfDocumentHost`，用 `StorageFile -> PdfDocument -> PdfPage -> InMemoryRandomAccessStream -> frozen BitmapFrame` 替换具体实现；调用方的窗口、预览和墨迹导出降级边界不变。
- `PdfPage.Size` 从 96-DPI DIP 换算为 PDF point；渲染目标按请求 DPI 计算，单边超过 16,384 像素或总量超过 32M 像素时返回 `null`，避免损坏/异常页面触发失控分配。
- 运行时构造的非二进制 fixture 覆盖：损坏 PDF 拒绝且可立即删除、Letter `612 x 792` point、96/144-DPI 输出 `816 x 1056` / `1224 x 1584`、白底黑矩形视觉像素、128 页最后一页尺寸，以及被系统归一到 14,400 point 后仍 fail-closed 的超大页面。
- 性能 focused 实测：预热后连续 3 次 Letter/96-DPI 渲染共 57.0 ms，平均 19.0 ms；测试预算为 5 秒，属于回归护栏而非跨设备 SLA。

### Fresh verification 与边界

- `dotnet test ... --filter "FullyQualifiedName~PdfDocumentHost"`：PASS，6/6；其中 1 项标记 `Gate=Performance`。
- `dotnet restore ClassroomToolkit.sln --locked-mode -m:1`：PASS。
- full Debug：build 0 warning / 0 error；stable tests 3006/3006（含 9 个性能预算）；CoreContract 29/29；hotspot、dependency vulnerability、dependency upgrade audit、`latest-all` analyzer 全部 PASS，analyzer 0 diagnostics。
- full 首次复跑暴露 WAL 并发用例首轮恢复 31/32；生产契约本就会保留单项非致命 I/O 失败供下次恢复，测试改为最多 3 次有界恢复，仍会阻断 Upsert 条目丢失或持续失败。原用例修改前独立连续运行 12/12 通过，修正后 focused 1/1、最终 full 全绿。
- 回滚需同时恢复 `PdfDocumentHost`、App/Test TFM、两个旧包引用与对应 lockfile，再运行 locked restore 和 full；不得只恢复 native 包而保留系统 API 实现。
- `repo_verified` 只证明当前 Windows 主机的 API 调用与基础合成结果；密码/签名/复杂字体或透明度 PDF、真实课件视觉、DPI、多屏、投影和课堂延迟仍未 `live_accepted`。
