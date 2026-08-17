# ClassroomToolkit 当前接手说明

最后更新：2026-08-17

## 当前主线

- 产品主链：启动 -> 名单/配置加载 -> 课堂操作 -> Interop 降级 -> 可观察结果与恢复。
- 代码侧没有已知 P0 阻断；发布仍需真实教室的多显示器、DPI、投影、PPT/WPS、触控和学生照片悬浮层验收。
- 本轮修复后的 standard Debug 已通过 2991 个普通测试、29 个核心契约和 hotspot，build 为 0 warning / 0 error；这仍是 `repo_verified`，不是 `live_accepted`。
- 历史重构任务图、自动循环和治理快照已退役；不要从 Git 历史恢复为日常门禁。

## 验证选择

- 普通局部改动：受影响测试 + `dotnet build ClassroomToolkit.sln -c Debug`。
- 共享 seam、Interop、窗口生命周期、持久化：`scripts/quality/run-local-quality-gates.ps1 -Profile standard`。
- 依赖变化或发布：同一入口使用 `-Profile full -Configuration Release`。
- 文档改动：`git diff --check` 和链接检查即可。

每个适用层只运行一次；已有精确当前结果时直接复用，不为普通改动重复 full。

## 本轮精简落点

- `ToolbarInteraction*` 已从 32 个文件收敛为 13 个，保留节流、重入和 dispatcher 降级。
- `Floating*` 已从 50 个文件收敛为 27 个，`ZOrderRequest*` 从 13 个收敛为 4 个；native executor 和真实 surface 行为仍保留。
- 文案、日志、私有方法名、资源 key 与历史治理脚本测试已退役；剩余源码形状契约以 Interop、生命周期、阻塞等待和持久化安全为主。
- JSON 设置的对象根节点校验已集中到存储 seam；schema 损坏、锁和短暂 IO 失败都会阻止覆盖，只有显式成功重载才解除阻断。
- `students.xlsx` 已改为损坏读取 fail-closed，且同一 store 的后续保存继续阻断；合法旧格式规范化前创建 `*.bak-normalize-<SHA256>.xlsx` 原字节备份。
- 零页 PDF 会在所有权转移前释放；`IPdfDocumentHost` 是后续替换归档 PDFium 的实现 seam，当前依赖替换仍开放。
- 原子写 fallback 已去掉非原子的覆盖复制与单调用者 policy；墨迹诊断文本和工作簿接线字符串断言由现有失败路径行为测试替代。
- INI 迁移已改为“纯内存迁移 + 保存前哈希备份”：加载无文件副作用，备份失败或冲突时拒绝覆盖。
- Paint 删除 4 个单表达式 policy 与对应复述测试；新的架构入口禁止继续按条件分支机械拆文件。
- standard 不运行墙钟微基准；full/focused 保留独占性能预算。依赖已删除 SourceGear 与测试重复固定，并完成 10.0.11/Test SDK 18.9.0 更新。
- WPS hook 已通过可注入后台队列把 4 个静态源码契约替换为行为测试；剩余直接涉及 Win32 native unhook 的形状契约只在建立安全 native adapter 后再退役。
- 后续只有出现独立失败或触及相应模块时，再按 deletion test 局部内联浅 policy；有生产 adapter + 测试 adapter 的 Interop seam 保留。

## 真值边界

- 代码、当前配置与 fresh 测试结果优先于历史文档。
- `repo_verified` 不等于课堂现场 `live_accepted`。
- 普通变更由 Git diff 和测试输出留痕；只有数据迁移、Interop 生命周期、发布或不可逆修复才新增 change-evidence。
