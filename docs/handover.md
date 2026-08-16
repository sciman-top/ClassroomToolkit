# ClassroomToolkit 当前接手说明

最后更新：2026-08-16

## 当前主线

- 产品主链：启动 -> 名单/配置加载 -> 课堂操作 -> Interop 降级 -> 可观察结果与恢复。
- 代码侧没有已知 P0 阻断；发布仍需真实教室的多显示器、DPI、投影、PPT/WPS、触控和学生照片悬浮层验收。
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
- JSON 设置的对象根节点校验已集中到存储 seam；数组等 schema 不兼容输入在加载和保存前检查中都会阻止覆盖。
- 原子写 fallback 已去掉非原子的覆盖复制与单调用者 policy；墨迹诊断文本和工作簿接线字符串断言由现有失败路径行为测试替代。
- 后续只有出现独立失败或触及相应模块时，再局部收敛 native adapter 或把静态安全契约替换为行为测试，不再启动全仓式重复审计。

## 真值边界

- 代码、当前配置与 fresh 测试结果优先于历史文档。
- `repo_verified` 不等于课堂现场 `live_accepted`。
- 普通变更由 Git diff 和测试输出留痕；只有数据迁移、Interop 生命周期、发布或不可逆修复才新增 change-evidence。
