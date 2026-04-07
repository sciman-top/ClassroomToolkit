# 2026-04-07 设置窗口可理解性优化（页签脏标记）

- rule_id: R1,R2,R6,R8
- risk_level: low
- scope: 点名设置 / 画笔设置 UI 可见性改进（不改业务参数语义）

## 依据
- 用户反馈：配置项较多导致理解困难，需要优化可读性并评估默认值策略。
- 现状代码中存在脏状态判断函数，但页签未显示改动提示。

## 变更
1. RollCallSettingsDialog：实现 `UpdateTabDirtyStates()`，为“显示/语音/遥控/提醒”页签追加 `*` 脏标记。
2. PaintSettingsDialog：实现 `UpdateSectionDirtyStates()`，为“基础/工具栏/兼容”页签追加 `*` 脏标记。
3. 新增通用 `SetTabHeader(...)` 辅助方法（两个窗口各自一份）。

## 命令与证据
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS（0 errors）

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS
- 备注：`PaintSettingsDialog.xaml.cs` 行数控制到 1878/1880（预算内）

## 异常与处理
- 一次并发执行测试时出现 `.NET Host` 文件锁冲突（CS2012），已改为串行重跑并通过。

## 回滚
- 回滚文件：
  - `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
- 回滚方式：撤销上述文件本次改动提交即可（不涉及数据迁移/配置格式变更）。

## 第二轮连续优化（无需首次提问）

### 目标
- 默认只显示常用设置，降低点名设置理解负担。
- 提供即时“改动摘要”，减少误改与确认焦虑。

### 变更
1. RollCallSettingsDialog UI
- 在设置页签上方新增“显示高级设置（语音/遥控/提醒）”开关，默认关闭。
- 默认仅展示“显示”页签，高级页签按需显示。
- 新增底部 `ChangeSummaryText`，实时显示“本次已修改哪些页签”。

2. RollCallSettingsDialog 逻辑
- 新增 `_advancedOptionsEnabled` 状态。
- 新增 `OnAdvancedOptionsChanged`、`UpdateAdvancedOptionsVisibility`、`SetTabVisibility`。
- 在 `UpdateTabDirtyStates()` 中联动更新页签脏标记与改动摘要。

### 命令与证据（第二轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS

### 平台异常留痕（platform_na）
- reason: 并发执行测试时出现 MSBuild/XAML 生成文件锁（`MainWindow.g.cs` / `PaintToolbarWindow.g.cs` 被占用）。
- alternative_verification: 改为串行顺序执行 `test -> contract/invariant` 后通过。
- evidence_link: 本文档“第二轮命令与证据”与终端日志。
- expires_at: 2026-04-14

## 第三轮连续优化（无需首次提问）

### 目标
- 在不引入首次启动提问的前提下，给用户提供“一键可理解”的配置入口。

### 变更
1. RollCallSettingsDialog 新增“常用模式”一键应用
- 新增模式选择：`课堂常用（推荐）`、`照片优先（展示）`、`遥控优先（翻页笔）`。
- 新增“应用”按钮，按模式批量回填相关设置项，降低逐项学习成本。

2. 与第二轮能力联动
- 应用模式后自动刷新：控件可用态、页签脏标记、底部改动摘要。

### 命令与证据（第三轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS

## 第四轮连续优化（无需首次提问）

### 目标
- 让“常用模式”可解释，避免用户不知道每个模式会改什么。

### 变更
1. RollCallSettingsDialog 增强常用模式可解释性
- 新增 `QuickProfileHintText`，随模式选择实时展示影响说明。
- 新增 `OnQuickProfileSelectionChanged` 与 `UpdateQuickProfileHint`。
- 模式应用后同步更新说明文本。

### 命令与证据（第四轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS

### 平台异常留痕（platform_na）
- reason: 并发执行门禁命令时，WPF 临时编译工程出现假性错误（`InitializeComponent` not found in `*_wpftmp.csproj`）。
- alternative_verification: 改为串行重跑 `contract/invariant`，结果通过。
- evidence_link: 本文档“第四轮命令与证据”与终端日志。
- expires_at: 2026-04-14

## 第五轮连续优化（画笔设置对齐）

### 目标
- 让画笔设置具备与点名设置一致的“常用模式 + 改动摘要”能力。

### 变更
1. PaintSettingsDialog UI
- 在“基础”页新增“常用模式”选择与“应用”按钮。
- 新增模式说明文本，实时解释每个模式的效果。
- 在底部操作栏上方新增“本次改动摘要”。

2. PaintSettingsDialog 逻辑（新增 partial 文件）
- 新增 `PaintSettingsDialog.QuickProfiles.cs`，实现：
  - 模式选择说明联动
  - 一键应用（课堂平衡 / 高稳定 / 高灵敏）
  - 改动摘要生成
- 主文件仅新增一行调用 `UpdateChangeSummaryText()`，避免热点文件膨胀。

### 命令与证据（第五轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS
- 备注：`PaintSettingsDialog.xaml.cs`=1879/1880（预算内）

## 第六轮连续优化（画笔高级项按需显示）

### 目标
- 进一步降低画笔设置首屏复杂度：默认仅展示基础配置，高级页按需显示。

### 变更
1. PaintSettingsDialog
- 新增 `PaintAdvancedOptionsCheck`：控制是否显示“工具栏/兼容”页签。
- 默认关闭，仅显示“基础”页；打开后显示高级页签。
- 保持“常用模式 + 模式说明 + 改动摘要”联动。

2. 代码组织
- 相关逻辑放在 `PaintSettingsDialog.QuickProfiles.cs`，热点主文件仅最小调用。

### 命令与证据（第六轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS（`PaintSettingsDialog.xaml.cs`=1880/1880）

### 平台异常留痕（platform_na）
- reason: 并发执行测试时再次触发 WPF `*_wpftmp.csproj` 假性编译错误（`InitializeComponent`/XAML 生成项缺失）。
- alternative_verification: 改为串行顺序重跑 `test -> contract/invariant` 后通过。
- evidence_link: 本文档“第六轮命令与证据”与终端日志。
- expires_at: 2026-04-14

## 第七轮优化（按反馈精修）

### 目标
- 去掉无效控件“显示高级设置（工具栏/兼容）”。
- 明确“常用模式”与“整套预设”的关系。
- 提升“基础”页分组清晰度。

### 变更
1. 去掉“显示高级设置（工具栏/兼容）”
- 删除 Paint 基础页该 CheckBox 及对应事件逻辑。
- 删除 `OnPaintAdvancedOptionsChanged` / `UpdatePaintAdvancedOptionsVisibility` / `SetTabVisibility`。

2. 明确模式关系文案
- 新增说明：`常用模式=一键快速套用；整套预设=可查看与手动调整同一套参数。`
- 调整“整套预设”说明文案，明确与上方作用于同一参数集。

3. 基础页分组优化
- 新增分组标题：`笔触与预设`、`基础微调`。
- 优化首屏阅读路径，降低认知跳转。

### 命令与证据（第七轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS（`PaintSettingsDialog.xaml.cs`=1879/1880）

### 平台异常留痕（platform_na）
- reason: 构建/测试期间出现运行中进程占用输出 DLL（`sciman Classroom Toolkit`）导致复制失败。
- alternative_verification: 结束占用进程后按硬门禁顺序重跑，全部通过。
- evidence_link: 本文档“第七轮命令与证据”与终端日志。
- expires_at: 2026-04-14

## 第八轮优化（按使用逻辑继续合并）

### 目标
- 保持兼容页常在前提下，继续按使用逻辑简化基础设置路径。

### 变更
1. 预设入口合并
- 将“白板笔预设 + 毛笔预设”合并为统一“笔触预设”入口。
- 依据笔触风格自动切换显示对应预设控件（白板/毛笔）。
- 毛笔高级仅在毛笔风格下显示。

2. 预设区按钮收敛
- 删除冗余按钮：`应用所选`、`恢复自定义`。
- 预设切换维持“选择即应用”的逻辑。
- 删除“套用推荐”按钮，仅保留推荐说明文本。

3. 基础微调顺序优化
- 调整为：`笔画粗细 -> 橡皮粗细 -> 不透明度`。

4. 清理
- 删除未使用字段 `_hasCustomManagedSnapshot`，消除编译告警。

### 命令与证据（第八轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS（0 warning, 0 error）

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS（`PaintSettingsDialog.xaml.cs`=1831/1880）

### 平台异常留痕（platform_na）
- reason: 中途构建阶段出现运行中 `sciman Classroom Toolkit` 占用输出文件（exe/dll）导致复制失败。
- alternative_verification: 结束占用进程后按硬门禁顺序重跑，全部通过。
- evidence_link: 本文档“第八轮命令与证据”与终端日志。
- expires_at: 2026-04-14

## 第九轮优化（书写模式并入整套预设）

### 目标
- 彻底消除“书写模式”独立入口，统一到“整套预设”中管理，避免用户认知分叉。

### 变更
1. PaintSettingsDialog 基础页结构
- 删除独立 `书写模式` 折叠区（`WritingModeOverrideExpander`）。
- 在 `整套预设` 卡片内直接放置 `书写模式` 下拉框。
- 书写模式说明文本统一保留在预设卡片内（`ClassroomWritingModeHint`）。

2. PaintSettingsDialog 逻辑清理
- 删除 `UpdateWritingModeOverrideExpanderState(...)` 及相关调用点。
- 预设切换和“转为自定义”流程仅保留一套路径，不再联动独立书写模式区域。

### 命令与证据（第九轮）
1. `dotnet build ClassroomToolkit.sln -c Debug`
- 结果：PASS（0 warning, 0 error）

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- 结果：PASS（3197 passed）

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- 结果：PASS（25 passed）

4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
- 结果：PASS（`PaintSettingsDialog.xaml.cs`=1814/1880）

### 回滚
- 回滚文件：
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs`
- 回滚方式：撤销本轮提交即可（无数据迁移，无配置格式变更）。
