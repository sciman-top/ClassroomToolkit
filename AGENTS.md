# AGENTS.md - ClassroomToolkit
**项目契约**: 2.0
**全局规则复核**: 9.57
**类型**: Windows WPF (.NET 10)
**最后更新**: 2026-07-15

## 1. 当前落点与目标归宿
- 当前落点：`ClassroomToolkit.sln` 是课堂教学工具主解决方案，现有 WPF/Interop/配置与数据兼容是运行真相。
- 目标归宿：持续交付课堂可用、可降级、可恢复的教师桌面工具，不以重构破坏既有用户数据和操作路径。
- 下一最小里程碑：完成当前任务最小 slice，并通过标准质量 profile 与 fresh evidence 收口。

## A. 仓库事实与模块边界
- `src/ClassroomToolkit.App`：WPF UI/启动；`Application`：用例编排；`Domain`：核心规则；`Services`：桥接；`Interop`：高风险系统接口；`Infra`：配置/存储/外部资源。
- `tests/ClassroomToolkit.Tests` 承载回归与契约；`scripts/quality/run-local-quality-gates.ps1` 是标准聚合门禁。
- 课堂可用性是最高不变量：不可崩溃或长时间卡死，外部依赖失败必须可降级。
- `Win32/COM/WPS/UIAutomation` 异常不得冒泡到 UI；触屏、窗口层级、hook 生命周期与设置加载是 hotspot。
- 不得破坏 `students.xlsx`、`student_photos/`、`settings.ini` 的格式、语义与向后兼容。

## B. 执行与风险边界
- Interop、全局 hook、窗口生命周期、持久化结构与课堂主路径属于高风险；先补/定位契约测试，再改实现。
- 本地 agent/IDE 配置、日志、缓存、运行态目录不属于“整理提交全部”；提交前按任务边界筛选。
- NuGet/.NET/系统组件变化必须说明必要性、平台基线与回滚；不得为纯规则改动更新依赖。
- 规则、profile、baseline 或 gate 变化前比对 README、CI、真实脚本与当前工作树；发现漂移先整合。

## C. 门禁、证据与回滚
- fixed order：`build -> test -> contract/invariant -> hotspot`。
- agent-rule contract CI：`.github/workflows/agent-rule-contract.yml` 只验证规则契约，不替代本仓产品门禁。
- build：`dotnet build ClassroomToolkit.sln -c Debug`
- test：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- contract/invariant：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- hotspot：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
- canonical full gate：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`。
- quick feedback 可用相关过滤测试；不得替代标准 profile。任一阶段失败或课堂 hotspot 未收敛即阻断。
- 证据放入 `docs/change-evidence/`，记录风险、命令、exit code、关键输出、兼容、N/A 与回滚。
- 回滚只撤销本任务切片；数据格式/持久化变化还必须提供备份、迁移逆向或兼容读取入口。

## D. Global Rule -> Repo Action
- `R1-R5`：先声明课堂场景、模块落点、目标和验证；止血兼容必须写回收点，不做无证据预抽象。
- `R6`：C 章命令与 standard profile 是交付门禁；quick 仅作反馈。
- `R7`：保护课堂主路径、Interop 生命周期和 `students.xlsx`/settings/photo 兼容。
- `R8`：`docs/change-evidence/` 承接依据、命令、证据和回滚。
- `E4`：standard profile/hotspot 承接健康；`E5`：依赖与系统组件变化记录供应链；`E6`：数据/配置变化必须有迁移、兼容和回滚。
