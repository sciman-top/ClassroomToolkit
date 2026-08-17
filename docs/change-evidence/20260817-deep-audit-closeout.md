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
- 当前 PDFium native 版本仍未替换，供应链风险保持开放；多显示器、DPI、投影、PPT/WPS、触控及真实 PDF 视觉仍需课堂 `live_accepted`。
