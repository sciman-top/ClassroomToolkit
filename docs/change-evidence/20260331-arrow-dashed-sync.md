规则ID=R1,R2,R3,R4,R6,R7,R8
影响模块=src/ClassroomToolkit.App/Paint
当前落点=PaintOverlayWindow.Ink.Geometry.cs / PaintOverlayWindow.Ink.cs（箭头几何与提交路径）
目标归宿=实箭头与虚箭头统一为“填充几何主导”的箭头渲染模型，虚箭头仅在箭身体现虚线分段
迁移批次=2026-03-31-batch-1
风险等级=中（仅影响 Arrow/DashedArrow 绘制分支）
执行命令=
1) codex status
2) codex --version
3) codex --help
4) Get-Command dotnet; Get-Command powershell; Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj
5) dotnet build ClassroomToolkit.sln -c Debug
6) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
7) dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
8) powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=
- build: PASS（0 error / 0 warning）
- test: PASS（3025 passed）
- contract/invariant: PASS（24 passed）
- hotspot: PASS（status=PASS）
- 变更文件:
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Geometry.cs
  - src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs
回滚动作=
- git restore --source=HEAD -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Geometry.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs

[N/A记录]
type=platform_na
reason=codex status 在当前非交互终端返回 "stdin is not a terminal"
alternative_verification=使用 codex --version 与 codex --help 完成平台能力与版本补证；并记录 active_rule_path=E:/CODE/ClassroomToolkit/AGENTS.md（项目级）
evidence_link=docs/change-evidence/20260331-arrow-dashed-sync.md
expires_at=2026-04-30

[最小诊断矩阵]
- cmd=codex status | exit_code=1 | key_output=Error: stdin is not a terminal | timestamp=2026-03-31T21:57:00+08:00
- cmd=codex --version | exit_code=0 | key_output=codex-cli 0.117.0 | timestamp=2026-03-31T21:57:00+08:00
- cmd=codex --help | exit_code=0 | key_output=Codex CLI usage displayed | timestamp=2026-03-31T21:57:00+08:00

[增量修复-2]
问题描述=虚箭头在箭头与箭杆连接处仍出现两个空心小点。
修复策略=
1) 虚箭头虚线段改为“从箭头端向后排布”，确保最后一段贴住箭头基部（notch）。
2) 虚箭头组合几何 FillRule 设置为 Nonzero，避免组合几何偶奇填充导致的局部空洞。
变更文件=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Geometry.cs

[增量复验]
- build: PASS（存在 MSB3026 警告，因 ClassroomToolkit 进程占用 exe，最终构建成功）
- test: PASS（3025 passed）
- contract/invariant: PASS（24 passed）
- hotspot: PASS

[风险与建议]
- 运行中的 ClassroomToolkit(29508) 会造成 apphost copy 重试警告；建议在后续全量质量门禁前关闭运行实例，消除噪音警告。

[增量修复-3]
问题描述=用户反馈虚箭头连接处仍有两个小空洞，并要求恢复箭杆起点正向排布。
修复策略=
1) 虚箭杆恢复为从起点到终点的正向虚线分段排布。
2) 对靠近箭头的最后一段施加小幅前向重叠（connectorOverlap），使其压入箭头头部几何，消除连接处针孔。
3) 保持 GeometryGroup.FillRule=Nonzero。
变更文件=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Geometry.cs

[增量复验]
- build: PASS
- test: PASS（3025 passed）
- contract/invariant: PASS（24 passed）
- hotspot: PASS

[增量修复-4]
问题描述=虚箭头连接处空洞放大，用户要求与实箭头统一方法。
根因=虚箭头仍由分离几何拼接（末段箭杆与箭头头部边界重合），抗锯齿下产生连接缝。
修复策略=
1) 虚箭头沿用实箭头同一头部参数（headLength/headHalfWidth/notch/shaftHalfWidth）。
2) 保留起点正向虚线排布，但将“最后一段箭杆 + 箭头头部”合并为单一闭合轮廓。
3) 前置虚线段保持分段矩形填充，不改变视觉节奏。
结果=虚箭头连接处不再依赖两块几何拼缝，理论上与实箭头同类稳定。
变更文件=
- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Geometry.cs

[增量复验]
- build: PASS
- test: PASS（3025 passed）
- contract/invariant: PASS（24 passed）
- hotspot: PASS
