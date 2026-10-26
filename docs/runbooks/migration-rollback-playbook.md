# 终态主线回滚手册（当前有效口径）

最后更新：2026-10-08

状态：active

## 1. 适用范围

- 本手册只覆盖当前“终态最佳架构”主线。
- 历史 `Phase 1-4` 回滚叙事已不再作为当前执行口径。
- 当前回滚优先级：发布标签 / 小批次提交回退 / 有效 feature flag 降级。

## 2. 回滚触发

- 课堂阻断故障：崩溃、卡死、放映控制失效、输入链路失效。
- 高风险主链出现无法在当前批次内自愈的窗口层级 / 激活 / 焦点回归。

## 3. 回滚手段优先级

### 3.1 首选：回退当前批次代码

1. 优先回退当前小批次提交或未发布变更。
2. 不跨多个不相关批次混回。
3. 回退后立即执行：
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - 高风险批次再执行：
     - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`

### 3.2 （已退役）SQLite 实验开关

- SQLite 业务存储实验栈已于 2026-10-26 整体退役（ADR-004 裁决），`CTOOLKIT_USE_SQLITE_BUSINESS_STORE` 与 `CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND` 不再存在，本节不再是可用回滚手段。
- 存储相关回滚统一走 3.1 代码回退；退役与回滚细节见 `docs/change-evidence/20261026-sqlite-experimental-stack-retirement.md`。

### 3.3 最后手段：回退到上一稳定发布标签

1. 回退到最近的稳定发布标签。
2. 不跨越多个未知批次做混合回退。
3. 回退后执行：
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`

## 4. 当前不应依赖的开关

以下开关当前不应再写入新的回滚方案：

- `CTOOLKIT_USE_APPLICATION_USECASES`
- `CTOOLKIT_USE_APPLICATION_PHOTO_FLOW`
- `CTOOLKIT_USE_APPLICATION_PAINT_FLOW`
- `CTOOLKIT_USE_APPLICATION_PRESENTATION_FLOW`

原因：

- 当前源码和测试中没有这些开关的定义或消费者。
- 它们不能作为冻结验收或线上故障的回滚总闸。

## 5. 回滚后必做检查

- `ArchitectureDependencyTests` 不新增违规。
- App -> Interop 文件数未上升。
- [project-status](../project-status.md) 与 [architecture](../architecture/README.md) 的状态描述仍然成立；若不成立，必须同步更新文档。
