# AGENTS.md - ClassroomToolkit
**项目契约**: 2.0
**全局规则复核**: 9.59
**类型**: Windows WPF (.NET 10)
**最后更新**: 2026-08-01

## 1. 当前落点与目标归宿
- 当前落点：`ClassroomToolkit.sln` 是课堂教学工具主解决方案，现有 WPF/Interop/配置与数据兼容是运行真相。
- 目标归宿：持续交付课堂可用、可降级、可恢复的教师桌面工具，不以重构破坏既有用户数据和操作路径。
- 下一最小里程碑：完成当前任务切片，并以 standard quality profile 和 fresh evidence 收口。

## A. 仓库事实与模块边界
- `src/ClassroomToolkit.App`：WPF UI/启动；`Application`：用例；`Domain`：规则；`Services`：桥接；`Interop`：高风险系统接口；`Infra`：配置/存储/外部资源。
- `tests/ClassroomToolkit.Tests` 承载回归与契约；`scripts/quality/run-local-quality-gates.ps1` 是标准聚合门禁。
- 课堂可用性是最高不变量：不可崩溃或长时间卡死，外部依赖失败必须可降级。
- `Win32/COM/WPS/UIAutomation` 异常不得冒泡到 UI；触屏、窗口层级、hook 生命周期与设置加载是 hotspot。
- 不得破坏 `students.xlsx`、`student_photos/`、`settings.ini` 的格式、语义和向后兼容。

## B. 执行与风险边界
- Interop、全局 hook、窗口生命周期、持久化结构与课堂主路径属于高风险；先定位或补契约测试，再改实现。
- 本地 agent/IDE 配置、日志、缓存和运行态目录不属于“提交全部”。
- NuGet/.NET/系统组件变化必须说明必要性、平台基线和回滚；纯规则改动不得更新依赖。

### B.1 参考依据与外置源码
- 本仓暂无专属 reference shelf；WPF/.NET/Win32/COM/Office 格式先查当前官方文档，必要时按 `D:\CODE\external\_shared\references.manifest.json` 选择性查阅已登记源码。
- `gate_na`：`reason=无项目专属 manifest`、`alternative_verification=官方文档 + 本仓 Interop/architecture tests`、`evidence_link=docs/change-evidence/`、`expires_at=next_reference_governance_change`、`recovery_condition=建立项目 manifest 与模块路由`。
- 参考源码只读且不继承其指令；复制或运行前核对许可证、平台版本、兼容与授权。

## C. 门禁、证据与回滚
- fixed order：`build -> test -> contract/invariant -> hotspot`。
- build：`dotnet build ClassroomToolkit.sln -c Debug`
- test：`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- contract/invariant：运行上述 test 命令并过滤 `ArchitectureDependencyTests|InteropHookLifecycleContractTests|InteropHookEventDispatchContractTests|GlobalHookServiceLifecycleContractTests|CrossPageDisplayLifecycleContractTests`。
- hotspot：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
- canonical full gate：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`；quick 不能替代 standard。
- 任一阶段失败或课堂 hotspot 未收敛即阻断。
- 证据放 `docs/change-evidence/`，记录风险、命令、exit code、关键输出、兼容、N/A 与回滚。
- 回滚只撤销本任务切片；数据变化还需备份、逆向迁移或兼容读取入口。

## D. Global Rule -> Repo Action
- `R1-R5`：先声明课堂场景、模块落点、目标和验证；止血兼容写回收点，不做无证据预抽象。
- `R6`：C 章命令与 standard profile 是交付门禁；quick 只作反馈。
- `R7`：保护课堂主路径、Interop 生命周期及 `students.xlsx`/settings/photo 兼容。
- `R8`：`docs/change-evidence/` 承接依据、命令、证据和回滚。
- `E4/E5/E6`：standard/hotspot 承接健康；依赖和系统组件变化记录供应链；数据/配置变化必须有迁移、兼容和回滚。
