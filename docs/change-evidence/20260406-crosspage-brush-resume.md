规则ID=R1/R2/R3/R6/R8
影响模块=src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs; tests/ClassroomToolkit.Tests/CrossPageInputResumeExecutionContractTests.cs
当前落点=跨页输入恢复（ResumeCrossPageInputOperationAfterSwitch）
目标归宿=跨页续笔首段即时回放并插值，避免首段直线与输入丢帧
迁移批次=20260406-1
风险等级=中
执行命令=
- dotnet build ClassroomToolkit.sln -c Debug
- dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~CrossPageInputResumeExecutionContractTests"
验证证据=
- 代码变更：HandlePointerMove 增加 consumed 分支；ResumeCrossPageInputOperationAfterSwitch 改为 bool 返回并消费 ShouldUpdateBrushAfterContinuation，执行 AppendInterpolatedBrushSamples + TryUpdateBrushStrokeGeometry。
- 最小回归：CrossPageInputResumeExecutionContractTests 2/2 Passed。
- gate_na:
  - type: gate_na
  - reason: build 阶段被本机进程锁定（sciman Classroom Toolkit PID 35724、Microsoft Visual Studio PID 25556 占用 src/ClassroomToolkit.App/bin/Debug/net10.0-windows/*.dll），导致完整门禁链无法继续。
  - alternative_verification: 运行最小契约测试子集（仅新增回归测试），确认关键调用链存在且可执行。
  - evidence_link: docs/change-evidence/20260406-crosspage-brush-resume.md
  - expires_at: 2026-04-08
  - recovery_plan: 关闭运行中的 Classroom Toolkit 与相关 VS 调试会话后，按固定顺序补跑 build -> test -> contract/invariant -> hotspot。
回滚动作=
- git restore --source=HEAD~1 src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs tests/ClassroomToolkit.Tests/CrossPageInputResumeExecutionContractTests.cs
