# 终态最佳架构最终验收清单

最后更新：2026-03-13  
状态：active

## 1. 进入最终冻结验收的前提

- 主方案、主进度、handover、边界图、Interop 台账口径一致。
- `ArchitectureDependencyTests` 通过。
- App -> Interop 直连数量未高于基线，且最好继续下降。
- Debug 全量测试通过。
- 高风险批次的 Release 全量测试通过。

## 2. 自动化验收

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`

## 3. 人工场景验收

### 3.1 核心场景

- PPT 全屏放映
- WPS 全屏放映
- 图片全屏
- PDF 全屏跨页
- 白板
- 图片 / PDF / 白板 / PPT-WPS 三者互切

### 3.2 窗口关系

- 启动器置顶关系
- 工具条置顶关系
- 点名窗口置顶与透明关系
- Overlay 激活、焦点与输入穿透
- ImageManager 激活与前台切换

### 3.3 Ink 与跨页

- 书写、擦除、翻页、恢复
- 跨页连续书写
- 不再依赖“拖动/缩放后才刷新”的补偿行为
- GPU 关闭时 CPU 路径稳定
- GPU 仅在双门控开启且能力满足时可启用；探测失败或运行异常必须回退 CPU

## 4. Feature Flag 验收口径

- 允许作为切换/回退手段的仅限：
  - `CTOOLKIT_USE_SQLITE_BUSINESS_STORE`
  - `CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND`
  - `CTOOLKIT_USE_GPU_INK_RENDERER`
  - `CTOOLKIT_ENABLE_EXPERIMENTAL_GPU_INK`
- 不再把 `CTOOLKIT_USE_APPLICATION_*` 视为冻结验收回退总闸。

## 5. 验收输出

- 填写：`docs/validation/templates/classroom-pilot-acceptance-template.md`
- 同步更新：
  - `docs/validation/2026-03-06-target-architecture-progress.md`
  - `docs/handover.md`
  - 如冻结成功，再补当前文档的结果区

## 6. 结果区

- 当前状态：自动化冻结复检已通过，自动化门已闭合，等待人工最终回归。
- 最近一次结论：`automated-freeze-recheck-after-gap-closure` 已通过（`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests"`=`5/5`；`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`=`2227/2227`；`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`=`2227/2227`，2026-03-13）。
- GPU Ink 可选发布策略收口：`ink-gpu-release-strategy-tail` 已完成（`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~Ink|FullyQualifiedName~Renderer|FullyQualifiedName~Brush|FullyQualifiedName~Gpu"`=`259/259`，2026-03-13）。
