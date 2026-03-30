# Target Architecture First Batch Implementation Plan

最后更新：2026-03-10  
状态：completed（Slice 1/2/3 已落地，且追加完成 `RollCallWindow.Input.cs` Interop 直连收口）

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在不扩大风险面的前提下，完成第一批终态架构收口，把 `PaintOverlayWindow.Presentation` 与 `RollCallWindow` 中最清晰的高风险接缝迁移到稳定边界。  

**Architecture:** 第一批不做整块重写，只做 3 个小切片：先抽走 `PaintOverlayWindow` 的放映命令路由，再抽走 `RollCallWindow` 的全局遥控钩子绑定/协调，最后把 `RollCallWindow.Windowing` 的扩展样式更新完全收进 `WindowStyleExecutor`。每个切片都以“先补测试，再最小实现，再跑定向验证”为准，完成后立刻收紧守卫和文档。  

**Tech Stack:** WPF, .NET 10, xUnit, FluentAssertions, Windowing/Session/Policy/Executor 模式

---

## 执行边界

- 本批只处理以下 3 个切片，不顺手扩展到 `MainWindow.*` 或跨页 Ink 主链。
- 本批不修改 `ClassroomToolkit.Interop` 公共契约，优先在 `App / Services / Windowing` 内部收口。
- 每完成一个切片，都要检查是否能从 `ArchitectureDependencyTests` 白名单中删掉对应文件。

## Slice 1 目标

- 把 `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs` 中“命令发送路由”从窗口代码后置抽出，形成独立可测的协调器。
- 本切片只动“命令路由与选路”，不改“焦点监控”“全屏检测”“WPS hook 生命周期”。

### Task 1: Overlay Presentation Command Router

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/OverlayPresentationCommandRouter.cs`
- Create: `tests/ClassroomToolkit.Tests/OverlayPresentationCommandRouterTests.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`

**Step 1: Write the failing test**

覆盖以下行为：

- 前台为 WPS 且 WPS 目标有效时，优先走 WPS
- 前台为 Office 且 Office 目标有效时，优先走 Office
- 当前类型为 WPS/Office 且两者都存在时，优先走当前类型
- 两者都存在但仅一方全屏时，优先走全屏方
- WPS/Office 都不可发时返回 false

建议测试骨架：

```csharp
public sealed class OverlayPresentationCommandRouterTests
{
    [Fact]
    public void TrySend_ShouldPreferForegroundWps_WhenForegroundIsWps()
    {
        // arrange
        // act
        // assert
    }
}
```

**Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayPresentationCommandRouterTests"
```

Expected: FAIL，提示新类型或测试尚未实现。

**Step 3: Write minimal implementation**

新增一个只负责路由的协调器，输入保持为窗口已具备的运行时信息，不自行读取 UI 状态：

```csharp
internal sealed class OverlayPresentationCommandRouter
{
    public bool TrySend(OverlayPresentationCommandRequest request)
    {
        // 只负责选路与调用发送委托
    }
}
```

窗口内先把以下方法的选路逻辑迁入协调器，再保留窗口对目标解析与状态采集：

- `TrySendPresentationCommand(...)`
- `TrySendWpsNavigation(...)`
- `TrySendOfficeNavigation(...)`
- `BuildWpsOptions(...)`

要求：

- 不改变 `PresentationControlService` 行为
- 不改变现有 `WpsNavigationDebouncePolicy` 行为
- 不新增新的 Interop 直连

**Step 4: Wire window to use the router**

- 在 `PaintOverlayWindow` 中持有协调器实例
- 让窗口只保留：
  - 运行时状态采集
  - `ResolveWpsTarget()` / Office target 解析
  - 最终调用 `_presentationService.TrySendToTarget(...)`

**Step 5: Run focused verification**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayPresentationCommandRouterTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationControlServiceTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationKeyCommandPolicyTests"
```

Expected: PASS

**Step 6: Check boundary impact**

- 检查 `PaintOverlayWindow.Presentation.cs` 是否仍需直接触达 `ClassroomToolkit.Services.Presentation` 或 `ClassroomToolkit.Interop.Presentation`
- 如果仅减少复杂度但未消除直连，不修改白名单，只更新切片状态

**Step 7: Commit**

```bash
git add src/ClassroomToolkit.App/Paint/OverlayPresentationCommandRouter.cs tests/ClassroomToolkit.Tests/OverlayPresentationCommandRouterTests.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs
git commit -m "refactor: 抽离 Overlay 放映命令路由"
```

## Slice 2 目标

- 把 `RollCallWindow.Input.cs` 中“翻页笔绑定解析 + hook 协调”从窗口类中拆出来。
- 本切片不改点名业务规则，不改 `_hookService` 接口，不改全局消息提示文案。

### Task 2: RollCall Remote Hook Binding And Coordinator

**Files:**
- Create: `src/ClassroomToolkit.App/RollCall/RollCallRemoteHookBindingPolicy.cs`
- Create: `src/ClassroomToolkit.App/RollCall/RollCallRemoteHookCoordinator.cs`
- Create: `tests/ClassroomToolkit.Tests/RollCallRemoteHookBindingPolicyTests.cs`
- Create: `tests/ClassroomToolkit.Tests/RollCallRemoteHookCoordinatorTests.cs`
- Modify: `src/ClassroomToolkit.App/RollCallWindow.Input.cs`

**Step 1: Write the failing tests**

`RollCallRemoteHookBindingPolicyTests` 覆盖：

- `f5` 特例返回 `F5 / Shift+F5 / Escape`
- 非法或已移除的 `W` 绑定回退到 fallback
- 普通按键正确解析为单个绑定

`RollCallRemoteHookCoordinatorTests` 覆盖：

- 点名模式关闭时不注册 hook
- 启用遥控翻页时注册 presenter hook
- 启用分组切换时注册 group-switch hook
- 注册失败时触发一次 unavailable 通知

建议接口骨架：

```csharp
internal static class RollCallRemoteHookBindingPolicy
{
    public static IReadOnlyList<KeyBinding> Resolve(string configuredKey, KeyBinding fallback) { }
}
```

```csharp
internal sealed class RollCallRemoteHookCoordinator
{
    public Task RestartAsync(RollCallRemoteHookRequest request) { }
}
```

**Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallRemoteHookBindingPolicyTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallRemoteHookCoordinatorTests"
```

Expected: FAIL

**Step 3: Implement binding policy**

- 把 `ResolveRemoteBindings(...)`
- `IsUnsupportedRemoteBinding(...)`

迁入 `RollCallRemoteHookBindingPolicy`

要求：

- 先不改窗口行为
- 行为与原逻辑字节级等价

**Step 4: Implement coordinator**

把以下职责迁入协调器：

- 根据 `IsRollCallMode / RemotePresenterEnabled / RemoteGroupSwitchEnabled` 决定是否注册
- 调用 `_hookService.RegisterHookAsync(...)`
- presenter/group-switch 两种 handler 的启动协调
- 注册失败时的 unavailable 通知节流

窗口保留：

- UI 线程上的具体动作：
  - `UpdatePhotoDisplay()`
  - `SpeakStudentName()`
  - `ShowGroupOverlay()`
  - `ShowRollCallMessage(...)`

**Step 5: Wire RollCallWindow to use the coordinator**

- `RollCallWindow.Input.cs` 只保留事件接入和 UI action 委托
- 删除窗口内重复的绑定解析与注册判断逻辑

**Step 6: Run focused verification**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallRemoteHookBindingPolicyTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallRemoteHookCoordinatorTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallWindowDiagnosticsPolicyTests"
```

Expected: PASS

**Step 7: Boundary check**

- 检查 `src/ClassroomToolkit.App/RollCallWindow.Input.cs` 是否还能继续减少 `ClassroomToolkit.Interop.Presentation` 直连
- 若可以彻底移除直连，更新 `ArchitectureDependencyTests` 白名单和 Interop 台账

**Step 8: Commit**

```bash
git add src/ClassroomToolkit.App/RollCall/RollCallRemoteHookBindingPolicy.cs src/ClassroomToolkit.App/RollCall/RollCallRemoteHookCoordinator.cs tests/ClassroomToolkit.Tests/RollCallRemoteHookBindingPolicyTests.cs tests/ClassroomToolkit.Tests/RollCallRemoteHookCoordinatorTests.cs src/ClassroomToolkit.App/RollCallWindow.Input.cs
git commit -m "refactor: 收口点名遥控钩子协调"
```

## Slice 3 目标

- 把 `RollCallWindow.Windowing.cs` 对 `NativeMethods.GwlExstyle` 的直接依赖消掉。
- 本切片只做“扩展样式执行口收口”，不改透明度判定策略。

### Task 3: RollCall Extended Style Execution Boundary

**Files:**
- Modify: `src/ClassroomToolkit.App/Windowing/WindowStyleExecutor.cs`
- Modify: `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/WindowStyleExecutorTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/App/RollCallTransparencyPolicyTests.cs`
- Modify: `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs`
- Modify: `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md`

**Step 1: Write the failing test**

补充 `WindowStyleExecutorTests`，覆盖“更新扩展样式位时，不需要调用方再传 `NativeMethods.GwlExstyle`”。

示例骨架：

```csharp
[Fact]
public void TryUpdateExtendedStyleBits_ShouldUseExtendedStyleIndex()
{
    // arrange
    // act
    // assert
}
```

**Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WindowStyleExecutorTests"
```

Expected: FAIL

**Step 3: Implement minimal executor change**

在 `WindowStyleExecutor` 中新增专用入口，例如：

```csharp
public static bool TryUpdateExtendedStyleBits(
    IntPtr hwnd,
    int setMask,
    int clearMask,
    out int updatedStyle)
{
    return TryUpdateStyleBits(hwnd, /* executor 内部选择 ex-style index */, setMask, clearMask, out updatedStyle);
}
```

要求：

- 不改变既有 `TryUpdateStyleBits(...)` 兼容性
- 仅把 `GwlExstyle` 常量引用收进执行器内部

**Step 4: Rewrite RollCallWindow to use the executor boundary**

- 让 `RollCallWindow.Windowing.cs` 改用新的 `TryUpdateExtendedStyleBits(...)`
- 删除该文件对 `ClassroomToolkit.Interop` 的直接 `using`

**Step 5: Run focused verification**

Run:

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WindowStyleExecutorTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallTransparencyPolicyTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"
```

Expected: PASS

**Step 6: Tighten guard and docs**

- 若 `RollCallWindow.Windowing.cs` 已不再含 `ClassroomToolkit.Interop`
  - 从 `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs` 白名单删除该文件
  - 在 `docs/architecture/2026-03-10-interop-direct-dependency-matrix.md` 中把该项状态改为已收口

**Step 7: Commit**

```bash
git add src/ClassroomToolkit.App/Windowing/WindowStyleExecutor.cs src/ClassroomToolkit.App/RollCallWindow.Windowing.cs tests/ClassroomToolkit.Tests/App/WindowStyleExecutorTests.cs tests/ClassroomToolkit.Tests/App/RollCallTransparencyPolicyTests.cs tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs docs/architecture/2026-03-10-interop-direct-dependency-matrix.md
git commit -m "refactor: 收紧点名窗口样式执行边界"
```

## 本批结束条件

- 至少完成 3 个切片中的前 2 个
- `ArchitectureDependencyTests` 通过
- 若切片导致白名单可收紧，必须同步收紧
- 不新增 App -> Interop 文件

## 本批统一验证

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~OverlayPresentationCommandRouterTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallRemoteHookBindingPolicyTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallRemoteHookCoordinatorTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WindowStyleExecutorTests"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"
```

高风险批次追加：

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release
```

## 本批不要做的事

- 不碰 `MainWindow.*`
- 不碰跨页 Ink 主链
- 不把 `CTOOLKIT_USE_APPLICATION_*` 再写成回滚方案
- 不新增新的 App -> Interop 白名单
