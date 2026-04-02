# 变更证据：倒计时丢秒与设置对话框分钟调节修复

- 日期：2026-03-31
- 规则 ID：R1/R2/R6/R8，A.3/A.4，C.2/C.5
- 风险等级：低（UI 交互与计时启动边界修复）

## 依据
- 用户反馈：
  - 倒计时启动后数字变化“丢秒”。
  - 倒计时设置窗口中滑动条最大到 25 分钟，但 `+` 和输入应支持到 150 分钟。
  - 输入分钟后 `+/-` 无法继续调节。

## 改动落点
- `src/ClassroomToolkit.App/RollCallWindow.Timer.cs`
  - 启动计时（暂停 -> 运行）时重启 `_stopwatch`，避免把未运行期间的时间计入首个 tick。
  - 重置计时、设置新倒计时后重启 `_stopwatch`，避免边界抖动。
- `src/ClassroomToolkit.App/TimerSetDialog.xaml.cs`
  - `+/-` 预览鼠标按下时先执行一次增减，修复普通点击不生效问题。
  - `SetMinutes` 统一 Clamp 到 `0..150` 并同步 `Minutes` 属性。
  - 分钟解析失败时，`+/-` 回退使用当前 `Minutes`，避免输入后按钮失效。

## 执行命令与结果
1. `codex status`  
   - exit_code: 1  
   - key_output: `Error: stdin is not a terminal`
2. `codex --version`  
   - exit_code: 0
3. `codex --help`  
   - exit_code: 0
4. `Get-Command dotnet; Get-Command powershell; Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`  
   - exit_code: 0
5. `dotnet build ClassroomToolkit.sln -c Debug`  
   - exit_code: 0
6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`  
   - exit_code: 0（3022 passed）
7. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`  
   - exit_code: 0（24 passed）
8. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`  
   - exit_code: 0（status=PASS）

## N/A 记录
- 类型：`platform_na`
- reason：`codex status` 在非交互终端执行，返回 `stdin is not a terminal`。
- alternative_verification：使用 `codex --version` 与 `codex --help` 作为平台诊断替代证据，并完成完整项目门禁链路验证。
- evidence_link：`docs/change-evidence/20260331-timer-countdown-and-dialog-fixes.md`
- expires_at：2026-04-30

## 回滚动作
1. 回滚文件：
   - `src/ClassroomToolkit.App/RollCallWindow.Timer.cs`
   - `src/ClassroomToolkit.App/TimerSetDialog.xaml.cs`
2. 执行门禁复验：
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

# Backfill 2026-04-03
当前落点=BACKFILL-2026-04-03
风险等级=BACKFILL-2026-04-03
规则ID=BACKFILL-2026-04-03
回滚动作=BACKFILL-2026-04-03
目标归宿=BACKFILL-2026-04-03
迁移批次=BACKFILL-2026-04-03
验证证据=BACKFILL-2026-04-03
影响模块=BACKFILL-2026-04-03
执行命令=BACKFILL-2026-04-03
