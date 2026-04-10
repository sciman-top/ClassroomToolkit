# 20260411 自动治理收尾留痕

## 目标
- 在不破坏既有行为与契约测试的前提下，持续推进可维护性、鲁棒性、性能微优化与代码去冗余。

## 主要变更
- Paint 导出模块分拆后缺口修复：补回 `ExecuteSessionCaptureExportCore`，保持导出行为与提示语义不变。
- PhotoOverlay 异步路径尝试优化后，因源码契约测试约束已回滚到契约兼容写法。
- `AppSettingsService` 持续去冗余：
  - 引入 `SetBool` 统一布尔序列化写法。
  - RollCall/Launcher/Paint 保存段落使用统一 helper。
  - `ResolveWhiteboardPreset`、`ResolveCalligraphyPreset`、`NormalizeInputMode` 清理重复分支。
  - `ParseList` 改为手写循环，减少 LINQ 中间分配。
- `FileLoggerProvider` 鲁棒性与性能微优化：
  - 按日志文件隔离写入失败，避免单文件异常影响整批。
  - 批量聚合时预估字典与 `StringBuilder` 容量。
  - 提取日志清理 best-effort 删除 helper。

## 门禁执行（固定顺序）
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 最新结果
- build: PASS（0 warning, 0 error）
- test(full): PASS（3213 passed）
- contract/invariant: PASS（25 passed）
- hotspot: PASS

## N/A 说明
- 本轮未使用 `platform_na` / `gate_na`。

## 回滚入口
- 变更文件均可通过 Git 按文件回退。
- 若导出/照片路径出现异常，优先回退：
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.*.cs`
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Settings/AppSettingsService*.cs`
  - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`

## 深层性能剖析（新增轮次）
- 采样时间：2026-04-11 00:48 - 00:50。
- 基线脚本：
  - `scripts/collect-brush-quality-baseline.ps1 -Configuration Debug -SkipRestore -SkipBuild`
  - `scripts/collect-brush-telemetry-report.ps1 -Configuration Debug -SkipRestore -SkipBuild`
- 基线产物：
  - `logs/brush-quality-baseline/20260411_004819/baseline-report.md`
  - `logs/brush-telemetry-report/20260411_004831/telemetry-report.md`

### 发现
- Brush 质量/性能守卫批次全部通过（34/34）。
- Telemetry（Calligraphy）`dt p95` 约 `0.005ms`，`alloc p95 = 0`，但存在 `alloc_max_bytes=22552` 的偶发峰值。
- `BrushPerformanceGuardTests` 5 连跑（优化前）总耗时：`[3498, 3715, 3517, 3397, 3429]ms`。

### 落地优化
- `MarkerBrushRenderer` 在 Ribbon 构建路径复用左右边界缓冲 `List<WpfPoint>`，避免每次构建几何时重复分配。
- 文件：`src/ClassroomToolkit.App/Paint/Brushes/MarkerBrushRenderer.cs`。

### 优化后复测
- Telemetry 产物：`logs/brush-telemetry-report/20260411_005024/telemetry-report.md`。
- `BrushPerformanceGuardTests` 5 连跑（优化后）总耗时：`[3482, 3333, 3240, 3256, 3326]ms`。
- 对比：中位数由 `3498ms` 降至 `3326ms`，约 **-4.9%**；最慢样本由 `3715ms` 降至 `3482ms`，约 **-6.3%**。

### 收敛验证
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build`：PASS（3213）。
- contract/invariant 子集：PASS（25）。
- hotspot：PASS。

### 新增优化（继续自动执行）
- `VariableWidthBrushRenderer`：Render 路径改为缓存复用 `SolidColorBrush`，避免每次渲染重复分配刷子对象。
- 变更文件：`src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.cs`。

### 本轮验证
- build：PASS。
- 性能相关测试子集（BrushPerformanceGuard / Ribbon / Telemetry）：PASS（12）。
- full test：PASS（3213）。
- contract/invariant：PASS（25）。
- hotspot：PASS。
