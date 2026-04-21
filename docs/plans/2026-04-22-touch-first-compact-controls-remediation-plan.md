# Touch-First Compact Controls Remediation Plan

日期：2026-04-22  
适用仓库：`D:\CODE\ClassroomToolkit`  
范围：`src/ClassroomToolkit.App` 高触达触屏界面，优先处理悬浮工具条、白板截图链路、滚动列表与图片管理器  
source of truth：本文件

## 1. 目标

本轮整改的目标不是把界面做大，而是在“尽量少遮挡教学内容”的前提下，把触屏主路径从“能用”提升到“稳定、低成本、低误触”。

目标拆解如下：

1. 工具条与常用浮层保持紧凑，默认不增加一级按钮数量。
2. 视觉尺寸与命中区解耦，允许透明热区更大，但同一区域按钮尺寸必须一致。
3. 工具条保持自由拖动，不做边缘吸附，只做屏幕可视范围夹紧。
4. 高优先级触屏路径不依赖鼠标语义、光标位置、细滚动条或隐式二次点击记忆。
5. 所有整改都必须带合同测试或人工验收清单，避免后续回退到桌面交互模型。

## 2. 硬约束

1. 一级工具条按钮数量保持不变，除非后续有明确证据证明某个按钮必须合并、替换或移出一级。
2. 工具条继续支持任意拖动到屏幕不同位置，不引入吸附、磁贴、自动贴边等行为。
3. 同一工具条内相同层级按钮的视觉尺寸统一；同一局部浮层内同层级按钮的视觉尺寸统一。
4. 紧凑优先，但命中区不得因为缩放而退化到明显不适合手指操作的水平。
5. 二级操作可以通过现有按钮展开，但不能要求用户依赖 tooltip、长按说明文案或状态记忆才能发现。

## 3. 运行事实

以下事实已经在代码中确认，整改必须以这些现状为前提：

1. 工具条缩放当前通过 `ToolbarContainer.LayoutTransform = new ScaleTransform(_uiScale, _uiScale)` 实现，属于整块布局缩放，不是只缩图标。
2. 工具条缩放范围当前为 `0.8 - 2.0`。
3. 白板工具条本地样式把图标按钮与切换按钮写成了 `30x30`，覆盖了共享样式中较大的触摸最小值。
4. 当前工具条拖动是自由拖动，仅对 `Left/Top` 做虚拟屏幕边界夹紧，没有吸附逻辑。
5. 白板按钮对应的 `BoardActionsPopup` 已经存在，但当前实现需要补齐显式打开路径。
6. 区域截图恢复/透传链路里仍存在依赖 `Cursor.Position` 的路径，这对触屏不是正确输入源。
7. 多个滚动界面仍未显式设置 `ScrollViewer.PanningMode`，WPF 默认值不足以支撑稳定的手指拖动滚动。

## 4. 架构决策

### 4.1 缩放决策

保留“工具条可缩放”的能力，但缩放只负责改变视觉密度，不允许把实际命中区线性缩小到不可接受。

落地方式：

1. 按钮模板拆成“外层命中容器 + 内层视觉容器”。
2. 外层命中容器保留统一最小命中下限。
3. 内层视觉容器跟随 `PaintToolbarScale` 缩放。
4. 同一工具条内所有主按钮共用同一组视觉尺寸和同一组命中区尺寸。

### 4.2 工具条信息架构决策

第一阶段不改一级按钮数量，只修正现有按钮的触屏可达性和二级操作表达方式。

落地方式：

1. `BoardButton` 继续承担截图/白板/底色入口，但必须显式打开现有二级面板。
2. 颜色与图形继续由现有按钮承担，不新增一级按钮，但要让“展开二级设置”的路径可见、可预期。
3. 工具条整体继续单行、紧凑、低遮挡，不扩展为多排常驻按钮。

### 4.3 拖动决策

保持自由拖动，不引入吸附。

落地方式：

1. 保留当前 `Left/Top` 更新逻辑与边界夹紧。
2. 新增显式触摸拖动路径，不能只依赖鼠标事件提升。
3. 调整拖动阈值与点击判定，避免轻点按钮时误触发拖动。

### 4.4 输入模型决策

触屏路径应优先使用真实触点，而不是光标位置或鼠标悬停状态。

落地方式：

1. 工具条点击重放、区域截图恢复、遮罩层透传统一基于最近一次真实指针点。
2. 需要兼容鼠标与触摸共存，但触摸不能退化为“假装是鼠标”。

## 5. 任务清单

## Task 1: 建立紧凑触屏尺寸基线

**Description:** 为紧凑工具条和相关浮层建立统一的视觉尺寸、命中区尺寸、拖动热区尺寸合同，明确“可以小看起来，但不能小到难点”。

**Acceptance criteria:**
- [ ] 主工具条按钮在同一区域使用统一视觉尺寸。
- [ ] 主工具条按钮在同一区域使用统一命中区尺寸。
- [ ] 最小缩放下命中区仍有明确下限，不随视觉尺寸一起无限缩小。

**Verification:**
- [ ] 更新或新增 xUnit 合同测试，覆盖工具条尺寸 token、最小命中区和缩放下限。
- [ ] 人工复核 `PaintToolbarWindow.xaml` 与共享样式，确认不再混用 `22/24/30/32` 命中区。

**Dependencies:** None

**Files likely touched:**
- `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- `tests/ClassroomToolkit.Tests/App/TouchFirstMetricsXamlContractTests.cs`

**Estimated scope:** M

## Task 2: 解耦工具条缩放与命中区

**Description:** 保留工具条缩放，但把“视觉缩放”和“命中区可用性”拆开，确保紧凑模式不会把手指热区缩没。

**Acceptance criteria:**
- [ ] `PaintToolbarScale` 继续生效。
- [ ] 工具条视觉尺寸随缩放变化，但命中区保留统一最小下限。
- [ ] 点击命中、按钮聚焦与现有点击重放路径在缩放后仍对齐。

**Verification:**
- [ ] `dotnet build ClassroomToolkit.sln -c Debug`
- [ ] 新增或更新工具条缩放合同测试，覆盖 `LayoutTransform` 与命中下限约束。
- [ ] 人工验证 `80% / 100% / 150% / 200%` 下工具条外观和点击区域一致。

**Dependencies:** Task 1

**Files likely touched:**
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.*`
- `tests/ClassroomToolkit.Tests/ToolbarScaleDefaultsTests.cs`

**Estimated scope:** M

## Task 3: 保持自由拖动并补齐触摸拖动路径

**Description:** 维持现有自由拖动定位，不做吸附；补齐一等公民的触摸拖动路径，降低“轻点变拖动”与“拖不动”的风险。

**Acceptance criteria:**
- [ ] 工具条仍然可以停在屏幕任意可视位置。
- [ ] 不引入吸附或自动贴边。
- [ ] 触摸拖动、鼠标拖动都能工作，且按钮轻点不会误判为拖动。

**Verification:**
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintToolbarDragModeContractTests|FullyQualifiedName~LauncherBubbleTouchContractTests"`
- [ ] 人工验证自由拖动、边界夹紧和轻点不误拖。

**Dependencies:** Task 1

**Files likely touched:**
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- `tests/ClassroomToolkit.Tests/PaintToolbarDragModeContractTests.cs`

**Estimated scope:** S

## Task 4: 不增加按钮数量，修正现有按钮的二级可达性

**Description:** 保持当前工具条按钮数量不变，通过现有按钮展开明确的二级面板，替代依赖记忆的二次点击与长按发现路径。

**Acceptance criteria:**
- [ ] `BoardButton` 可以显式打开现有操作面板。
- [ ] 快捷颜色与图形按钮的“进入二级设置”路径可见、可预期。
- [ ] 一级按钮数量和工具条分组结构保持不变。

**Verification:**
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintToolbarTouchSettingsContractTests|FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests|FullyQualifiedName~ToolbarSecondTapIntentPolicyTests"`
- [ ] 人工验证“截图 / 白板 / 底色”“颜色切换”“图形切换”都能在第一次接触时理解。

**Dependencies:** Task 1, Task 2

**Files likely touched:**
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.TouchFirstActions.cs`
- `src/ClassroomToolkit.App/Paint/ToolbarSecondTapIntentPolicy.cs`
- `tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs`
- `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`

**Estimated scope:** M

## Task 5: 修正截图恢复与点击重放的触点来源

**Description:** 让区域截图恢复、透传与工具条点击重放统一使用真实输入点，移除触摸链路对 `Cursor.Position` 的依赖。

**Acceptance criteria:**
- [ ] 区域截图恢复不再依赖光标旧位置。
- [ ] 工具条点击重放使用最近一次真实输入点。
- [ ] 触摸与鼠标共存时，触摸路径不会被鼠标状态污染。

**Verification:**
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RegionCaptureWhiteboardIntegrationContractTests"`
- [ ] 手工验证触摸取消截图、恢复截图、透传点击时的落点正确。

**Dependencies:** Task 2, Task 3

**Files likely touched:**
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
- `src/ClassroomToolkit.App/Paint/RegionSelectionOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/MainWindow.Paint.cs`
- `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`

**Estimated scope:** M

## Task 6: 批量补齐触摸滚动主路径

**Description:** 为主要滚动容器显式设置触摸平移，让“拖内容区滚动”成为默认主路径，减少对细滚动条的依赖。

**Acceptance criteria:**
- [ ] 主要滚动容器显式设置合适的 `PanningMode`。
- [ ] 手指拖内容区可以完成滚动。
- [ ] 不需要放大成厚重滚动条，也不允许把滚动条当唯一可用路径。

**Verification:**
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~TouchFirstMetricsXamlContractTests|FullyQualifiedName~DialogTouchFlowContractTests"`
- [ ] 人工验证设置页、班级选择、学生列表、图片管理器主要滚动区。

**Dependencies:** Task 1

**Files likely touched:**
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
- `src/ClassroomToolkit.App/ClassSelectDialog.xaml`
- `src/ClassroomToolkit.App/StudentListDialog.xaml`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
- `tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs`

**Estimated scope:** M

## Task 7: 图片管理器进入紧凑触屏收口

**Description:** 在不引入额外常驻按钮的前提下，统一图片管理器的角标、工具条、缩略图交互热区和操作路径，并处理大目录下的滚动/点击稳定性。

**Acceptance criteria:**
- [ ] 同一区域按钮尺寸一致。
- [ ] 小角标采用大热区承接点击，不再要求精细点按。
- [ ] 缩略图滚动与点击在大目录下保持稳定，不出现明显错点和卡顿。

**Verification:**
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerTouchFlowContractTests|FullyQualifiedName~ImageManagerActivationPolicyTests|FullyQualifiedName~PhotoTouchInputContractTests"`
- [ ] 人工验证“进入文件夹 -> 滚动 -> 预览 -> 收藏/删除”触屏路径。

**Dependencies:** Task 1, Task 6

**Files likely touched:**
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.*.cs`
- `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`
- `tests/ClassroomToolkit.Tests/ImageManagerActivationPolicyTests.cs`

**Estimated scope:** L

## Task 8: 次高频对话框与页面统一收口

**Description:** 修正点名、计时器、设置类对话框中的小按钮、分散操作和多步输入问题，统一到紧凑但可触的密度标准。

**Acceptance criteria:**
- [ ] 同一对话框页脚按钮尺寸一致。
- [ ] 高频操作在不增加遮挡的前提下减少精细点按。
- [ ] 计时器、点名、列表选择等典型流程能够稳定单手触摸完成。

**Verification:**
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~DialogTouchFlowContractTests"`
- [ ] 人工验证点名、计时器、班级选择三条主路径。

**Dependencies:** Task 1, Task 6

**Files likely touched:**
- `src/ClassroomToolkit.App/RollCallWindow.xaml`
- `src/ClassroomToolkit.App/TimerSetDialog.xaml`
- `src/ClassroomToolkit.App/ClassSelectDialog.xaml`
- `tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs`

**Estimated scope:** M

## Task 9: 补齐行为级回归防线

**Description:** 将本次整改的关键交互约束写成测试，避免后续有人只看视觉效果又把触屏路径改回鼠标路径。

**Acceptance criteria:**
- [ ] 关键尺寸、缩放、拖动、二级面板可达性、触点来源、滚动主路径都有测试守护。
- [ ] 新测试能准确表达“不增一级按钮、不做吸附、热区不随视觉无限缩小”的约束。
- [ ] 关键热点页面至少有一条人工验收清单落档。

**Verification:**
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

**Dependencies:** Task 1-8

**Files likely touched:**
- `tests/ClassroomToolkit.Tests/*`
- `docs/validation/*`

**Estimated scope:** M

## 6. 实施顺序

建议按以下顺序执行：

1. Task 1 -> Task 2 -> Task 3  
先把尺寸基线、缩放语义、自由触摸拖动三件事收敛，否则后续每个页面都会重复返工。

2. Task 4 -> Task 5  
接着修正最核心的白板/截图工具条主路径，优先打掉隐式规则和错误触点来源。

3. Task 6  
统一滚动模型，把“手指拖内容区滚动”补成全局主路径。

4. Task 7 -> Task 8  
再处理图片管理器和次高频页面，统一密度和触摸稳定性。

5. Task 9  
最后把本轮约束固化到测试和验收文档。

## 7. 检查点

### Checkpoint A: Task 1-3 后

- [ ] 工具条仍保持单行紧凑。
- [ ] 工具条可缩放、可自由拖动、无吸附。
- [ ] 最小缩放下依然能稳定手指点击。

### Checkpoint B: Task 4-6 后

- [ ] 白板、截图、颜色、图形二级路径可显式发现。
- [ ] 区域截图恢复与点击重放触点正确。
- [ ] 主要滚动容器均可通过内容区拖动完成滚动。

### Checkpoint C: Task 7-9 后

- [ ] 图片管理器与次高频页面完成尺寸收口。
- [ ] 关键触屏合同测试全部通过。
- [ ] 手动验收清单可覆盖课堂高频操作。

## 8. 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| 视觉尺寸与命中区解耦后，点击区域与视觉边界不一致，导致误解 | 高 | 对外层透明热区做统一规则，并在热点页面做人工命中验证 |
| 工具条触摸拖动与按钮点击冲突 | 高 | 引入明确拖动阈值和拖动句柄优先级，不把所有按钮区域都当拖动区 |
| 二级操作显式化后破坏现有熟练用户节奏 | 中 | 保留现有一级按钮位置和数量，只改可达性，不改主功能分组 |
| 图片管理器改动过多，牵出性能回归 | 高 | 先做热区和路径统一，再独立处理虚拟化/调度问题 |
| 滚动主路径修正后与现有鼠标逻辑冲突 | 中 | 触摸路径显式分支，鼠标与触摸分别验证 |

## 9. 非目标

本轮不做以下事项：

1. 不整体重做视觉风格。
2. 不先行增加新的一级工具条按钮。
3. 不引入工具条吸附、自动贴边、自动收纳等新行为。
4. 不在没有验证依据的情况下大改功能入口信息架构。

## 10. 最小开工包

如果下一轮直接开做，第一批应只执行以下内容：

1. Task 1：尺寸基线合同与共享样式基线。
2. Task 2：缩放与命中区解耦。
3. Task 3：自由触摸拖动。
4. Task 4：`BoardButton`、颜色、图形的二级可达性。

这样可以最快满足当前用户已明确的四个约束：

1. 工具条紧凑。
2. 热区可大于视觉尺寸。
3. 按钮数不变。
4. 工具条自由拖动、不做吸附。
