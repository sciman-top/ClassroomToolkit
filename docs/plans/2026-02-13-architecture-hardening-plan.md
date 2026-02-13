# ClassroomToolkit 架构加固 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在不影响课堂稳定性的前提下，分阶段完成 High DPI 适配、DI 贯通与 MainWindow 渐进式 MVVM 化，降低耦合与回归风险。

**Architecture:** 采用“先稳定性后架构”的顺序：先落地 PerMonitorV2 与跨屏验证，再抽象窗口创建为工厂并接入 DI，最后将 MainWindow 的可绑定状态与命令迁入 MainViewModel。全过程保持外部行为兼容，使用小步提交与可回滚切片。

**Tech Stack:** .NET 8, WPF, Microsoft.Extensions.DependencyInjection, xUnit, FluentAssertions, PowerShell

---

### Task 1: 建立回归基线与验收清单

**Files:**
- Create: `docs/plans/2026-02-13-architecture-hardening-checklist.md`
- Modify: `docs/plans/2026-02-13-architecture-hardening-plan.md`

**Step 1: 编写基线检查清单（手工验收）**

```markdown
- 画笔开关、点名开关、照片教学入口、最小化与恢复
- Overlay/Toolbar/RollCall/ImageManager 的置顶与 Owner 链
- 设置保存与重启恢复（paint/launcher/roll-call）
```

**Step 2: 记录当前关键文件与热点函数**

Run: `rg -n "class MainWindow|ApplyZOrderPolicy|EnsurePaintWindows|SaveSettings" src/ClassroomToolkit.App/MainWindow*.cs`
Expected: 输出 MainWindow 各分部中的关键函数位置，作为迁移前锚点

**Step 3: 执行一次完整构建与测试作为基线**

Run: `dotnet build ClassroomToolkit.sln -c Debug`
Expected: `Build succeeded.`

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
Expected: 测试通过（或记录已有失败项，不新增失败）

**Step 4: Commit**

```bash
git add docs/plans/2026-02-13-architecture-hardening-checklist.md docs/plans/2026-02-13-architecture-hardening-plan.md
git commit -m "docs: 建立架构改造基线与验收清单"
```

### Task 2: High DPI（PerMonitorV2）落地与跨屏验证

**Files:**
- Create: `src/ClassroomToolkit.App/app.manifest`
- Modify: `src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
- Modify: `docs/plans/2026-02-13-architecture-hardening-checklist.md`

**Step 1: 新增 manifest 并启用 PerMonitorV2**

```xml
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
  </windowsSettings>
</application>
```

**Step 2: 在 csproj 指定 ApplicationManifest**

```xml
<PropertyGroup>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

**Step 3: 构建并执行跨屏手工验证**

Run: `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug`
Expected: `Build succeeded.`

Run: `dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
Expected: 主界面、画笔覆盖层在 4K+投影组合场景下无明显模糊/错位

**Step 4: Commit**

```bash
git add src/ClassroomToolkit.App/app.manifest src/ClassroomToolkit.App/ClassroomToolkit.App.csproj docs/plans/2026-02-13-architecture-hardening-checklist.md
git commit -m "fix(ui): 启用 PerMonitorV2 并补充跨屏验收"
```

### Task 3: DI 贯通窗口创建（先工厂后替换）

**Files:**
- Create: `src/ClassroomToolkit.App/Paint/IPaintWindowFactory.cs`
- Create: `src/ClassroomToolkit.App/Paint/PaintWindowFactory.cs`
- Create: `src/ClassroomToolkit.App/Photos/IImageManagerWindowFactory.cs`
- Create: `src/ClassroomToolkit.App/Photos/ImageManagerWindowFactory.cs`
- Modify: `src/ClassroomToolkit.App/App.xaml.cs`
- Modify: `src/ClassroomToolkit.App/MainWindow.xaml.cs`
- Modify: `src/ClassroomToolkit.App/MainWindow.Paint.cs`
- Modify: `src/ClassroomToolkit.App/MainWindow.Photo.cs`

**Step 1: 为 Overlay/Toolbar/ImageManager 定义工厂接口与实现**

```csharp
public interface IPaintWindowFactory
{
    (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create();
}
```

**Step 2: 在 App.xaml.cs 注册工厂服务**

Run: 在 `ConfigureServices()` 中添加 `AddSingleton<IPaintWindowFactory, PaintWindowFactory>()` 与 `AddSingleton<IImageManagerWindowFactory, ImageManagerWindowFactory>()`
Expected: `MainWindow` 可通过构造函数注入工厂

**Step 3: 将 MainWindow 中直接 new 替换为工厂调用**

Run: 替换 `new Paint.PaintOverlayWindow()` / `new Paint.PaintToolbarWindow()` / `new ImageManagerWindow(...)`
Expected: 行为不变，生命周期与事件绑定保持一致

**Step 4: 构建并回归关键路径**

Run: `dotnet build ClassroomToolkit.sln -c Debug`
Expected: `Build succeeded.`

Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
Expected: 测试通过且不新增失败

**Step 5: Commit**

```bash
git add src/ClassroomToolkit.App/App.xaml.cs src/ClassroomToolkit.App/MainWindow.xaml.cs src/ClassroomToolkit.App/MainWindow.Paint.cs src/ClassroomToolkit.App/MainWindow.Photo.cs src/ClassroomToolkit.App/Paint/IPaintWindowFactory.cs src/ClassroomToolkit.App/Paint/PaintWindowFactory.cs src/ClassroomToolkit.App/Photos/IImageManagerWindowFactory.cs src/ClassroomToolkit.App/Photos/ImageManagerWindowFactory.cs
git commit -m "refactor(app): 用工厂与 DI 接管窗口创建"
```

### Task 4: MainWindow 渐进 MVVM（状态与命令先行）

### Task 5: 抽离窗口编排（Z-Order/Owner/Topmost）协调器

### Task 6: 收尾、发布前验证与回滚预案

## 执行结果（2026-02-13）
- 已完成 Task 1~Task 6 的代码与自动化验证。
- Debug: `dotnet build`/`dotnet test` 均通过。
- Release: `dotnet build`/`dotnet test` 均通过。
- 脚本验证: `powershell -File scripts/ctoolkit.ps1 -SkipCommit` 通过。
- 未完成项: 4K+投影及课堂真实流程人工验收需在线下设备执行。

## 回滚锚点（按任务粒度）
- Task 6 之前: `996bf98`
- Task 5 之前: `5c3c969`
- Task 4 之前: `ab808a2`
- Task 3 之前: `04347c1`
- Task 2 之前: `0bb793a`
- Task 1 之前: `820c6d8`
