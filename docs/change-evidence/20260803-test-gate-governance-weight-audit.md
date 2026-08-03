# 测试、门禁与治理重量审计

日期：2026-08-03
范围：测试项目、quality/validation scripts、CI、治理真值文档、NuGet lock files；不改变课堂运行路径、数据格式或外部行为。

## 审计结论

确认存在治理过重，而且根因不是业务回归用例总量，而是治理层叠加了重复和非确定性检查：

- 旧 `standard` 先跑 3544 个全量测试，再重复运行其中 29 个 contract。
- 每次质量门禁先启动 3 次 dry-run 来验证 stable-test 脚本自身；同一形状又被 C# 源码文本测试锁定。
- `quick`、`standard`、`full` 的治理阶段完全相同，`standard/full` 的测试集合也相同，profile 名称没有真实成本边界。
- patch 级依赖有更新即可阻断日常代码交付；2026-08-03 基线在 build 0 warning/error、3544/3544 和 contract 29/29 通过后，仍于约 60 秒处失败。
- 代码门禁读取 host-local `logs/`，会把工作树之外的运行历史当成代码正确性。
- 治理自检强制要求未实际承载本仓 CI 的 Azure/GitLab 占位文件存在。
- `tests/.tmp` 累积 9849 个文件和超过 1.2 万个目录；清理器只枚举目录，且无法删除带只读属性的历史条目。
- 868 个测试文件中约 144 个会读取源码/XAML，475 个声明测试涉及 source contract。多数用于 WPF/Interop 静态安全边界，不能仅按数量删除；本次只退役已证明重复的 7 个治理源码形状测试。

## 修复

- 保留固定 `build -> test -> contract/invariant -> hotspot` 顺序，用 `Gate=CoreContract` Trait 将 5 组核心契约与普通回归互斥切分。
- `quick` 只做固定四段的精选反馈；`standard` 做全量互斥测试并扫描漏洞；`full` 再做依赖升级和 `latest-all` analyzer 审计。
- stable tests 和 contract 均使用前置 build 产物，不再各自 restore/build。
- 退役重复 dry-run validator、自检式 truth-source gate、两组对应源码形状测试以及 Azure/GitLab 占位 wrapper。
- 日志阈值脚本保留为 operator diagnostic，但不再影响源码门禁。
- 发布 workflow 与 release 文档改用 `full`；日常交付保留 `standard`。
- 更新 ClosedXML 0.105.1、SourceGear.sqlite3 3.53.4、SQLitePCLRaw 3.0.5 并刷新 lock files；跨 major 项继续由已有有效 waiver 管理。
- 临时根维护改为文件/目录统一计数，递归清除只读属性后 best-effort 删除；回归覆盖陈旧文件、混合 soft limit 和只读嵌套文件。

## Fresh evidence

- 定向回归：`TestPathHelperTests` 8/8；环境入口相关定向集合共 24/24。
- 实际缓存维护：`tests/.tmp` 从超过 2.2 万个条目下降到 643 个；目录外批量删除被宿主策略拦截后，使用正常测试入口完成安全回收。
- `standard Debug`：build 0 warning/error；普通回归 3508/3508；core contract 29/29；hotspot PASS；漏洞扫描 PASS；总耗时约 37 秒。
- `full Debug`：上述阶段全部 PASS；依赖升级审计仅剩有效 waiver 项；latest-all analyzer `total=0`；总耗时约 62 秒。
- 相比旧基线，日常 standard 从约 60 秒后失败变为约 37 秒通过；发布深审仍保留供应链和 analyzer 保障。

## 风险与回滚

- 风险：profile 语义变化会影响依赖旧 `quick/full` 等价行为的外部调用者；仓库内调用、CI 和发布文档已同步。
- 回滚：仅回滚本证据列出的 scripts、CI、测试 Trait/临时清理、依赖版本和文档；随后重新 restore 并执行 standard/full。
- live acceptance：本切片只改变工程验证与测试缓存维护，不替代课堂现场验收。
