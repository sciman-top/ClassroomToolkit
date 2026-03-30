# 终态主线回滚手册（当前有效口径）

最后更新：2026-03-11  
状态：active

## 1. 适用范围

- 本手册只覆盖当前“终态最佳架构”主线。
- 历史 `Phase 1-4` 回滚叙事已不再作为当前执行口径。
- 当前回滚优先级：发布标签 / 小批次提交回退 / 有效 feature flag 降级。

## 2. 回滚触发

- 课堂阻断故障：崩溃、卡死、放映控制失效、输入链路失效。
- 高风险主链出现无法在当前批次内自愈的窗口层级 / 激活 / 焦点回归。
- SQLite 或 GPU 实验开关导致主路径不稳定。

## 3. 回滚手段优先级

### 3.1 首选：回退当前批次代码

1. 优先回退当前小批次提交或未发布变更。
2. 不跨多个不相关批次混回。
3. 回退后立即执行：
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - 高风险批次再执行：
     - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`

### 3.2 次选：关闭仍然有效的实验开关

- 业务存储：
  - `CTOOLKIT_USE_SQLITE_BUSINESS_STORE=0`
  - `CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND=0`
- GPU Ink：
  - `CTOOLKIT_USE_GPU_INK_RENDERER=0`
  - `CTOOLKIT_ENABLE_EXPERIMENTAL_GPU_INK=0`

说明：

- 以上开关当前仍有实际作用。
- 当前已选定并闭环的下一 SQLite 业务域是学生名册（student workbook / class roster）；该域已具备 bridge 失败时回读 SQLite snapshot 的兜底路径，回滚口径保持“先回退当前批次代码，再必要时关闭 SQLite 实验开关”不变。
- 学生名册之后的历史向业务域 Ink 历史快照（ink stroke history snapshot，含按文档/页索引的笔迹历史）已切换到 SQLite 主路径；当前回滚顺序为“先回退当批代码，再按需关闭 SQLite 实验开关”，并保留 bridge/source 失败时的兼容回读。
- 关闭后需补跑对应定向验证与全量 Debug。

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

- 这些开关目前仅在 `AppFlags` 中保留定义，未形成可靠的主链切换闭环。
- 它们不能作为冻结验收或线上故障的回滚总闸。

## 5. 回滚后必做检查

- `ArchitectureDependencyTests` 不新增违规。
- App -> Interop 文件数未上升。
- 主方案 / 主进度 / handover 的状态描述仍然成立；若不成立，必须同步更新文档。
