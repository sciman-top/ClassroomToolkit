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
