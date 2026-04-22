# 2026-04-22 系统化审查与定向优化（StudentPhotoResolver 缓存失效粒度）

## 基本信息
- `issue_id`: `review-20260422-student-photo-cache`
- `attempt_count`: `1`
- `clarification_mode`: `direct_fix`
- `clarification_scenario`: `N/A`
- `rule_ids`: `R1,R2,R3,R6,R8`
- `risk_level`: `Low`
- `boundary`: `src/ClassroomToolkit.App/Photos` + `tests/ClassroomToolkit.Tests`
- `current_destination`: `InvalidateStudentCache` 整班级缓存清空（`studentId` 参数未参与失效粒度）
- `target_destination`: 仅失效目标学生缓存项，保留同班其他已索引项，减少无谓索引重建

## 变更摘要
1. `StudentPhotoResolver.InvalidateStudentCache` 改为按学生粒度失效：
   - 增加 `studentId` 规范化与空值防御。
   - 仅删除命中学生在目录缓存索引中的项。
   - 保留同目录其它缓存项，避免整目录重复构建。
2. 增加回归测试：
   - 仅移除目标学生缓存，不影响同班其他学生。
   - 非法 `studentId` 不应导致整班缓存被误清空。

## 执行命令与证据
- `codex --version` -> `exit_code=0`，`codex-cli 0.122.0`
- `codex --help` -> `exit_code=0`，帮助输出正常
- `codex status` -> `exit_code=1`，`stdin is not a terminal`
- `dotnet list ClassroomToolkit.sln package --outdated` -> `exit_code=0`，无可升级包（源下）
- `dotnet list ClassroomToolkit.sln package --vulnerable` -> `exit_code=0`，无漏洞包
- `dotnet list ClassroomToolkit.sln package --deprecated` -> `exit_code=0`，无弃用包
- `powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug` -> `exit_code=0`
  - `build/test/contract/hotspot/dependency-governance` 全通过
- `dotnet test ... --filter "FullyQualifiedName~StudentPhotoResolverTests"` -> `exit_code=0`，`22 passed`
- `dotnet build ClassroomToolkit.sln -c Debug` -> `exit_code=0`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` -> `exit_code=0`，`3418 passed`
- `dotnet test ... --filter "ArchitectureDependencyTests|InteropHookLifecycleContractTests|InteropHookEventDispatchContractTests|GlobalHookServiceLifecycleContractTests|CrossPageDisplayLifecycleContractTests"` -> `exit_code=0`，`28 passed`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1` -> `exit_code=0`

## N/A 记录
### `platform_na`
- `reason`: `codex status` 在非交互会话下返回 `stdin is not a terminal`，无法获得交互状态页
- `alternative_verification`: 使用 `codex --version` 与 `codex --help` 作为最小平台诊断替代，并完成完整仓库门禁链验证
- `evidence_link`: 本文件“执行命令与证据”章节
- `expires_at`: `2026-06-30`

### `gate_na`
- 本次无 `gate_na`

## hotspot 人工复核结论
- 复核文件：
  - `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
- 关注点：
  - 并发更新下 `_cache.TryUpdate` 竞争行为是否可收敛
  - 失效粒度改变后是否引入旧数据残留
  - `studentId` 非法输入是否触发误清缓存
- 结论：未发现破坏兼容性或行为回归风险，逻辑与测试一致。

## 回滚方案
1. 回滚目标文件：
   - `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
   - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
2. 回滚动作：
   - 还原 `InvalidateStudentCache` 为整目录失效逻辑
   - 删除新增两条测试和缓存读取辅助方法
3. 回滚后复测：
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
