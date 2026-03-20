# Terminal Architecture Closure Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不做全仓大改的前提下，把当前仓库收口到“终态最佳架构”的可验收状态。  
**Architecture:** 采用“定点抽离 + 守卫收紧 + 批次验收”路线：先冻结新增结构债，再拆热点，再下沉业务，再收紧边界。每一批都必须有可复现验证和可执行回滚。  
**Tech Stack:** .NET 10, WPF, xUnit, FluentAssertions, Microsoft.Extensions.DependencyInjection

---

## Chunk 1: 终态验收清单（可打勾）

- [ ] `App` 层不再新增 `Infra/Interop` 直连，现有 allowlist 每批至少减少 1 项。  
- [ ] `MainWindow.*` 只保留 UI 展示与交互编排，不再新增业务规则/状态推进。  
- [ ] `PaintOverlayWindow.Photo.CrossPage.cs` 拆分后单文件不超过 `1200` 行。  
- [ ] `RollCallViewModel` 不直接承担工作簿装载与引擎构建细节，改由 `Application` 用例承接。  
- [ ] `Services` 不新增第二业务中心倾向（仅做桥接/编排）。  
- [ ] `ArchitectureDependencyTests`、热点契约测试、改动相关测试全部通过。  
- [ ] 保持 `students.xlsx`、`student_photos/`、`settings.ini/json` 兼容。  
- [ ] Interop 失败路径仍可降级，不向 UI 冒泡致命异常。  

---

## Chunk 2: 分批任务表（中小规模，非全仓重构）

### Task 1: 先冻结结构债（守卫收紧）

**Files:**
- Modify: `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs`
- Test: `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs`

- [ ] **Step 1: 增加“禁止新增 App->Domain/Application 直连”预算守卫（按当前基线）**
- [ ] **Step 2: 运行守卫测试并确认通过**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"`
- [ ] **Step 3: 记录当前 allowlist 与后续缩减目标**
- [ ] **Step 4: Commit**
```bash
git add tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs
git commit -m "test: 冻结架构边界基线并防新增回堆"
```

**回滚点:** 回退本次守卫增强 commit。

### Task 2: 拆解 CrossPage 热点（第一优先）

**Files:**
- Modify: `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`
- Create: `src/ClassroomToolkit.App/Paint/CrossPage*Policy.cs`（按现有命名风格拆分）
- Test: `tests/ClassroomToolkit.Tests/CrossPage*Tests.cs`（补齐新增策略测试）

- [ ] **Step 1: 先写/补失败测试，覆盖拆分目标行为（导航、刷新、去重、延迟派发）**
- [ ] **Step 2: 最小移动代码到新策略文件，不改行为**
- [ ] **Step 3: 运行 CrossPage 相关测试**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPage"`
- [ ] **Step 4: 复测热点契约**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintOverlayPresentationTargetRoutingContractTests|FullyQualifiedName~PaintOverlayDrawingStateContractTests"`
- [ ] **Step 5: Commit**
```bash
git add src/ClassroomToolkit.App/Paint tests/ClassroomToolkit.Tests
git commit -m "refactor: 拆解 CrossPage 热点并保持行为不变"
```

**回滚点:** 按 commit 粒度回退；若拆分中断，至少保证测试与编译恢复。

### Task 3: RollCall 业务下沉到 Application

**Files:**
- Create: `src/ClassroomToolkit.Application/UseCases/RollCall/RollCallSessionLoadUseCase.cs`
- Modify: `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs`
- Modify: `src/ClassroomToolkit.App/RollCallWindow.xaml.cs`
- Test: `tests/ClassroomToolkit.Tests/*RollCall*Tests.cs`

- [ ] **Step 1: 先写失败测试（UseCase 输入输出契约）**
- [ ] **Step 2: 在 Application 实现最小可用装载用例（不含 UI 细节）**
- [ ] **Step 3: 改造 ViewModel 调用 UseCase，移除直接细节拼装**
- [ ] **Step 4: 运行 RollCall 相关测试**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCall"`
- [ ] **Step 5: Commit**
```bash
git add src/ClassroomToolkit.Application src/ClassroomToolkit.App tests/ClassroomToolkit.Tests
git commit -m "refactor: 下沉 RollCall 装载编排到 Application 用例"
```

**回滚点:** 先回退 App 侧改动 commit，再回退 UseCase 引入 commit。

### Task 4: MainWindow 收口与 allowlist 缩减

**Files:**
- Modify: `src/ClassroomToolkit.App/MainWindow.xaml.cs`
- Modify: `src/ClassroomToolkit.App/Services/PaintWindowOrchestrator.cs`
- Modify: `tests/ClassroomToolkit.Tests/ArchitectureDependencyTests.cs`
- Test: `tests/ClassroomToolkit.Tests/App/*MainWindow*Tests.cs`

- [ ] **Step 1: 把 MainWindow 中新增的流程判断收敛到既有 Policy/Coordinator**
- [ ] **Step 2: 缩减至少 1 项 allowlist（Infra/Interop 或 Domain 直连）**
- [ ] **Step 3: 运行 MainWindow + 架构守卫测试**
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindow|FullyQualifiedName~ArchitectureDependencyTests"`
- [ ] **Step 4: Commit**
```bash
git add src/ClassroomToolkit.App tests/ClassroomToolkit.Tests
git commit -m "refactor: 收口 MainWindow 编排并缩减边界白名单"
```

**回滚点:** 逐 commit 回退，优先回退 allowlist 收紧改动。

---

## Chunk 3: 每批统一验证与发布前验收

- [ ] **批次内最小验证:** 仅跑改动相关测试，必须全绿。  
- [ ] **批次完成验证:**  
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`  
- [ ] **发布前验证:**  
Run: `dotnet build ClassroomToolkit.sln -c Release`  
Run: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`  
- [ ] **人工验收（必做）:** WPS 全屏输入、跨页墨迹、RollCall 首开与设置保存、DPI 跨屏清晰度。  

---

## Chunk 4: 7天排期建议（可直接执行）

- [ ] **D1-D2:** Task 1 + Task 2（先防新增，再拆最大热点）  
- [ ] **D3-D4:** Task 3（RollCall 业务下沉）  
- [ ] **D5:** Task 4（MainWindow 收口 + allowlist 缩减）  
- [ ] **D6:** Debug 全量测试 + 人工冒烟  
- [ ] **D7:** Release 构建测试 + 交付记录（影响模块/验证/回滚）  

---

## 完成判定（DoD）

当以下条件全部满足，即可判定“达到终态最佳架构收口目标”：

- [ ] 热点文件拆解达标（CrossPage 文件降到目标线以下）。  
- [ ] `App` 层边界守卫从“防新增”进入“可持续缩减”状态。  
- [ ] `RollCall` 主流程由 `Application` 用例主导，UI 仅绑定与调度。  
- [ ] Debug/Release 测试通过，人工高风险场景通过。  
- [ ] 每批有独立回滚点，可单独撤销。  

