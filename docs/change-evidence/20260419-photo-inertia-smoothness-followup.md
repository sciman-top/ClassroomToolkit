# 2026-04-19 Photo Inertia Smoothness Follow-up

- rule_ids: `R1`, `R2`, `R6`, `R8`
- risk_level: `medium`
- boundary: `src/ClassroomToolkit.App/Paint` + `tests/ClassroomToolkit.Tests`
- current_landing: `PaintOverlayWindow` 全屏 PDF/图片拖拽惯性链路
- target_destination: 触摸甩动与鼠标甩动均获得更稳定、连续的惯性滚动

## Changes

- `PhotoPanReleaseTuning` 增加速度采样参数（采样窗口、最大样本年龄、最小位移阈值、近期权重）。
- `PhotoPanReleaseTuningPolicy` 按输入源区分采样容忍度：触摸路径放宽采样年龄/窗口并提高近期权重，降低低频触屏丢惯性概率。
- `PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity` 改为使用 pointer-kind 对应的采样调参，不再固定用鼠标常量。
- 新增 `PhotoPanInertiaMotionPolicy.TryResolveInertiaStep`，在每帧使用“当前速度 + 下一帧速度”的平均速度求位移，降低惯性尾段突兀感。
- `PaintOverlayWindow.Photo.Transform.PanInertia` 改为调用 `TryResolveInertiaStep`，统一单帧位移与减速更新。
- 补充/更新回归测试，覆盖触摸采样容忍度与惯性步进行为。

## Commands

1. `codex --version`
2. `codex --help`
3. `codex status`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoPanInertiaMotionPolicyTests|FullyQualifiedName~PhotoPanReleaseTuningPolicyTests|FullyQualifiedName~PhotoManipulationInertiaPolicyTests|FullyQualifiedName~PhotoPanInertiaDefaultsTests"`
5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoPanInertiaMotionPolicyTests|FullyQualifiedName~PhotoPanReleaseTuningPolicyTests"`
6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoPanInertiaMotionPolicyTests|FullyQualifiedName~PhotoPanReleaseTuningPolicyTests|FullyQualifiedName~PhotoPanInertiaDefaultsTests|FullyQualifiedName~PhotoManipulationInertiaPolicyTests"`
7. `dotnet build ClassroomToolkit.sln -c Debug`
8. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
9. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## Key Output

- `codex --version`: `codex-cli 0.121.0`
- `codex --help`: succeeded
- `codex status`: failed with `stdin is not a terminal`
- 定向测试（步骤 4）: `19 passed`
- 红灯验证（步骤 5）: 编译失败，缺少 `PhotoPanReleaseTuning` 新采样字段（符合先红后绿）
- 定向回归（步骤 6）: `22 passed`
- `build`: passed, `0 warnings`, `0 errors`
- 全量测试: `3324 passed`, `0 failed`
- contract/invariant: `28 passed`, `0 failed`

## Platform N/A

- type: `platform_na`
- reason: `codex status` 在当前非交互 shell 中返回 `stdin is not a terminal`
- alternative_verification: 采用 `codex --version` + `codex --help` 作为平台诊断替代，并记录命令输出
- evidence_link: `docs/change-evidence/20260419-photo-inertia-smoothness-followup.md`
- expires_at: `2026-05-19`

## Hotspot Review

- reviewed_files:
  - `src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuningPolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs`
- findings: `none`
- conclusions:
  - 触摸路径的释放速度估计不再被鼠标采样阈值卡死，低采样触屏下惯性触发更稳定。
  - 惯性每帧位移改为平均速度积分，减速尾段更平顺，避免“最后几帧突停”体感。
  - 鼠标路径仍保持原有阈值基线，仅在帧积分方式上受益于平滑步进。

## Rollback

1. `git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.PanInertia.cs src/ClassroomToolkit.App/Paint/PhotoPanInertiaDefaults.cs src/ClassroomToolkit.App/Paint/PhotoPanInertiaMotionPolicy.cs src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuning.cs src/ClassroomToolkit.App/Paint/PhotoPanReleaseTuningPolicy.cs tests/ClassroomToolkit.Tests/PhotoPanInertiaDefaultsTests.cs tests/ClassroomToolkit.Tests/PhotoPanInertiaMotionPolicyTests.cs tests/ClassroomToolkit.Tests/PhotoPanReleaseTuningPolicyTests.cs`
2. 删除证据文件：`docs/change-evidence/20260419-photo-inertia-smoothness-followup.md`
