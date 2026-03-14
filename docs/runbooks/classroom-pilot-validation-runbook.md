# Classroom Pilot Validation Runbook

最后更新：2026-03-10  
状态：active

## 目标
执行终态主线人工验收：课堂试点 + 指标对比 + 冻结前确认。

## 操作步骤
1. 执行基础门禁：
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - `dotnet build ClassroomToolkit.sln -c Release`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release --no-build`
2. 试点窗口内采集指标：
   - `powershell -File scripts/validation/collect-pilot-metrics.ps1 -WindowMinutes 30`
3. 依据模板记录：
   - `docs/validation/templates/classroom-pilot-acceptance-template.md`
4. 填写最终验收：
   - `docs/validation/target-architecture-final-acceptance.md`
5. 同步主文档：
   - `docs/validation/2026-03-06-target-architecture-progress.md`
   - `docs/handover.md`

## 回退触发
- 核心指标连续两个窗口超阈值。
- 出现课堂阻断故障（崩溃/卡死/控制失效）。

## 回退执行
- 参考：`docs/runbooks/migration-rollback-playbook.md`
