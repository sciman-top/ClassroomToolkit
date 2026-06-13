# 2026-06-13 README 与文档入口状态刷新

## 范围

- 当前落点：`README.md`、`README.en.md`、`使用指南.md`、`docs/README.md`、`docs/handover.md`、`docs/tech-debt-backlog.md`
- 目标归宿：把项目入口文档同步到 2026-06-13 的真实仓库状态，尤其是最近两批课堂能力变化、照片叠加层失败分支收口、当前完整测试快照和推荐阅读顺序

## 规则与风险

- 规则：R1、R2、R6、R8；本仓文档留痕规则；`build -> test -> contract/invariant -> hotspot` 的状态必须如实记录
- 风险等级：低
- 兼容性：不改数据格式、不改发布脚本；照片叠加层失败分支沿用透明穿透运行态语义

## 本次更新

- 根 README 中英双语同步到最新状态：
  - 补入 5 月底到 6 月初的高频课堂能力变化
  - 明确 2026-06-13 的本地验证快照
  - 明确完整测试集已恢复全绿
- 教师使用指南补充：
  - 图片 / PDF 全屏下的白板入口行为
  - 快捷画笔二次点击的颜色 + 粗细选择
  - 图片 / PDF 批注撤销和白板内工具切换说明
  - 计时设置标签与常见问题更新
- `docs/README.md` 改为“当前真相入口”导向，优先指向 README、handover、backlog 和最近 evidence
- `docs/handover.md` 从旧的历史重构交接说明，改为当前工作区接手说明
- `docs/tech-debt-backlog.md` 将照片叠加层失败分支契约与实现收口标记为完成

## 验证依据

- `dotnet build ClassroomToolkit.sln -c Debug`
  - 结果：通过，0 warning / 0 error
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - 结果：3533 通过，0 失败
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayLoadFailureBranchContractTests|FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests|FullyQualifiedName~PhotoOverlayTopmostNoActivateContractTests|FullyQualifiedName~RollCallWindowPhotoOverlayReuseContractTests"`
  - 结果：16/16 通过
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - 结果：29/29 通过
- `git diff --check`
  - 结果：通过，仅有既有 LF/CRLF 归一化提示

## 根因与收口

- 根因：`PhotoOverlayWindow` 已经把空 bitmap 失败分支切到 `EnterInactivePassthroughState()`，但 `PhotoOverlayLoadFailureBranchContractTests` 仍断言旧的 `Hide()`。
- 收口：契约测试改为要求 `EnterInactivePassthroughState()`，并明确该分支不应重新调用 `Hide()`。
- 热点复核：`ApplyLoadedBitmap`、`EnterInactivePassthroughState`、`WindowTopmostExecutor` 与照片叠加层置顶 / 穿透路径保持封装，Interop 异常仍不冒泡到 UI。

## N/A

- `gate_na`：课堂现场验证未执行；原因是本次提交范围为代码契约、文档同步和本地门禁收口，不启动真实 PPT / WPS、多显示器或投影现场流程
- `alternative_verification`：`build -> test -> contract/invariant -> hotspot` 本地链路通过，并同步核对 README、handover、backlog 与本 evidence 使用同一测试口径
- `evidence_link`：本文件
- `expires_at`：下次完整测试结果、发布状态或课堂现场验证结果发生变化时

## 回滚

- 回滚本次修改的文档文件即可恢复旧入口口径
- 如果后续测试状态变化，不建议回滚到旧文档，而应基于新事实继续前推更新
