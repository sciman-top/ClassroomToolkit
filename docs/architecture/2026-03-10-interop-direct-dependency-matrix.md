# App -> Interop 直连债务台账（6 文件基线）

最后更新：2026-03-11  
状态：active  
对应守卫：`tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs`

## 1. 目的

- 把当前 `6` 个 App -> Interop 直连文件从“隐式白名单”改成“显式债务台账”。
- 明确哪些是允许保留的稳定边界，哪些只是历史债。
- 为后续收口提供逐文件的目标归宿与验证口径。

## 2. 分类规则

- `Keep`：允许作为长期稳定边界保留，但只能维持现有职责，不得扩张。
- `Shrink`：历史债，必须继续上收或迁移。
- `Review`：暂不扩张，也不直接视为终态稳定边界；需在相邻收口中一起处理。

## 3. 当前基线

- 守卫当前允许基线：`6`
- 原则：只允许下降，不允许新增
- 说明：下表中的“目标归宿”是当前终态收口口径；若后续新增 ADR，以 ADR 为准
- 自动化冻结复检（2026-03-13）：`ArchitectureDependencyTests` 通过（`5/5`），且全量 Debug/Release 均通过（`2227/2227`），基线仍为 `6`

## 4. 逐文件台账

| 文件 | 分类 | 现状 | 目标归宿 | 最小验证 |
| --- | --- | --- | --- | --- |
| `src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs` | Done | Interop 探测逻辑已下沉到 `Services.Presentation.PresentationDiagnosticsProbe`，App 诊断层不再直连 Interop（2026-03-10） | 保持不回流；诊断 UI 层仅做结果汇总与呈现 | `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/Paint/OverlayWindowStyleBitsPolicy.cs` | Done | 样式位常量依赖已迁移到 `WindowStyleBitMasks`（2026-03-10） | 保持不回流；后续只通过 `Windowing` 样式位边界访问 | `OverlayWindowStyleBitsPolicyTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs` | Done | Interop 全限定名已迁移为全局类型可见方式，文件本身不再直连 Interop（2026-03-10）；2026-03-11 已补做 overlay presentation tail 闭环验证 | 保持不回流；放映编排继续通过窗口边界与服务能力协同 | `OverlayPresentationCommandRouterTests` / `PresentationChannelAvailabilityPolicyTests` / `PresentationFullscreenTypeResolutionPolicyTests` / `PresentationFocusRestorePolicyTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs` | Done | Interop using/限定名已下沉到全局可见边界，文件本身不再直连 Interop（2026-03-10） | 保持不回流；窗口代码继续只通过受控边界访问能力 | `ArchitectureDependencyTests` + Presentation/Windowing 定向测试 |
| `src/ClassroomToolkit.App/Paint/WpsHookInterceptPolicy.cs` | Done | 拦截策略已改为纯 App 语义输入（`isRawSendMode`），不再直连 Interop 枚举（2026-03-10） | 保持不回流；策略层仅处理拦截决策 | `WpsHookInterceptPolicyTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/Paint/WpsWheelRoutingPolicy.cs` | Done | 路由策略已改为纯 App 语义输入（`isWpsForeground`），不再依赖 Interop 类型（2026-03-10） | 保持不回流；策略层仅处理旁路判定 | `WpsWheelRoutingPolicyTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/RemoteKeyDialog.xaml.cs` | Done | 按键文本校验已下沉到 `Services.Input.KeyBindingTokenParser`，对话框不再直连 Interop（2026-03-10） | 保持不回流；对话框仅处理 UI 与错误提示 | `KeyBindingTokenParserTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs` | Done | 遥控按键校验/规范化已改用 `Services.Input.KeyBindingTokenParser`，窗口层不再直连 Interop（2026-03-10） | 保持不回流；设置窗口仅处理配置输入输出 | `KeyBindingTokenParserTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/RollCallWindow.Input.cs` | Done | 遥控钩子绑定与注册适配已收口到 `RollCallRemoteHookCoordinator` + `RollCallWindow.xaml.cs` 受控注册入口（2026-03-10） | 保持不回流；后续仅通过受控 coordinator + 注册适配入口访问 hook 能力 | `RollCallRemoteHook*` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs` | Done | 直连已收口到 `WindowStyleExecutor` 边界（2026-03-10） | 保持不回流；后续仅通过 Windowing 执行器访问样式位能力 | `RollCallTransparencyPolicyTests` / `WindowStyleExecutorTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/RollCallWindow.xaml.cs` | Done | 遥控 hook 键位解析已下沉到 `GlobalHookService`，窗口层不再直连 Interop（2026-03-10） | 保持不回流；窗口仅保留 UI 壳与 coordinator 接线 | `RollCallRemoteHookCoordinatorTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/Windowing/NativeCursorWindowGeometryInteropAdapter.cs` | Keep | 光标窗口几何适配器 | 保持 Adapter 边界 | 对应 Windowing 定向测试 |
| `src/ClassroomToolkit.App/Windowing/NativeWindowPlacementInteropAdapter.cs` | Keep | 窗口位置适配器 | 保持 Adapter 边界 | `WindowPlacement*` 测试 |
| `src/ClassroomToolkit.App/Windowing/NativeWindowStyleInteropAdapter.cs` | Keep | 样式位适配器 | 保持 Adapter 边界 | `WindowStyle*` 测试 |
| `src/ClassroomToolkit.App/Windowing/NativeWindowTopmostInteropAdapter.cs` | Keep | Topmost 适配器 | 保持 Adapter 边界 | `WindowTopmost*` 测试 |
| `src/ClassroomToolkit.App/Windowing/PresentationForegroundSuppressionInteropAdapter.cs` | Keep | 放映前台抑制适配器 | 保持 Adapter 边界 | Presentation/Windowing 定向测试 |
| `src/ClassroomToolkit.App/Windowing/RollCallTransparencyPolicy.cs` | Done | Interop 样式位常量依赖已改为 `WindowStyleBitMasks`（2026-03-10） | 保持不回流；策略层继续只做透明度决策，不直连 Interop | `RollCallTransparencyPolicyTests` / `ArchitectureDependencyTests` |
| `src/ClassroomToolkit.App/Windowing/WindowHandleValidationInteropAdapter.cs` | Keep | 句柄校验适配器 | 保持 Adapter 边界 | `WindowHandleValidation` 相关测试 |
| `src/ClassroomToolkit.App/Windowing/WindowPlacementExecutor.cs` | Done | SWP 常量依赖已迁移到 `WindowPlacementBitMasks`，执行器不再直连 Interop（2026-03-10） | 保持不回流；执行器仅负责重试与执行，不承载 Interop 常量引用 | `WindowPlacementExecutorTests` / `ArchitectureDependencyTests` |

## 5. 优先收口顺序

### P0

- `SystemDiagnostics.cs`
- Windowing Adapter 边界稳定性回归（保持 `6` 基线不反弹）

### P1

- `MainWindow.*` 场景链路收口（非 Interop 直连维度）

### P2

- `PaintOverlayWindow.*` 剩余状态散点收口（非 Interop 直连维度）

## 6. 收口完成条件

- 文件完全脱离 `ClassroomToolkit.Interop`，并从守卫白名单移除。
- 相应职责已有新的稳定归宿。
- 定向测试通过。
- 若影响高风险主链，再补全量 Debug；必要时补全量 Release。
