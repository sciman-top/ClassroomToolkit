# 无现场前提兼容发布基线（sciman课堂工具箱）

最后更新：2026-03-18  
适用范围：无法立即做教室现场验收时的发布前最小闭环

## 1. 目标

- 在不做人工现场（双屏/课堂）验收的前提下，最大化降低“系统不兼容、运行库缺失、演示控制失效”风险。
- 输出可直接交付部署同事执行的标准化步骤。

## 2. 兼容边界（当前口径）

- 系统：Windows 10 22H2+ / Windows 11 22H2+。
- 架构：x64。
- 运行时：`.NET Desktop Runtime 10.x`（标准版需要；离线版自带）。
- 发布架构：`win-x64`（禁止发布 x86）。

## 3. 自动化门禁（必须通过）

按顺序执行：

1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet build ClassroomToolkit.sln -c Release`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`
5. `powershell -File scripts/release/preflight-check.ps1`

判定规则：

- 任一命令失败则禁止发布。
- 若出现仅警告（如文件锁重试），需复跑到无失败结果并记录原因。

## 4. 发布产物基线（必须满足）

1. 生成标准版（FDD）+ 离线版（SCD）：
   - `powershell -File scripts/release/prepare-distribution.ps1 -Version <版本号> -EnsureLatestRuntime`
2. 标准版必须包含：
   - `启动.bat`
   - `bootstrap-runtime.ps1`
   - `prereq/windowsdesktop-runtime-10.x-win-x64.exe`
   - 说明：`bootstrap-runtime.ps1` 仅做运行时检测与手动安装指引，不执行自动静默安装。
3. 两个包都必须包含：
   - `SHA256SUMS.txt`
   - 使用说明文档

## 5. 运行库与原生依赖检查（必须满足）

对发布目录做最小文件检查：

1. 标准版（FDD）至少存在：
   - `ClassroomToolkit.App.runtimeconfig.json`（引用 `Microsoft.WindowsDesktop.App 10.x`）
   - `x64/pdfium.dll`
   - `e_sqlite3.dll`
2. 离线版（SCD）至少存在：
   - `hostfxr.dll`
   - `coreclr.dll`
   - `vcruntime140_cor3.dll`
   - `e_sqlite3.dll`

## 6. 演示控制兼容基线（无现场版）

1. 保持默认降级策略开启：
   - `presentation_lock_strategy_when_degraded = true`
2. 标准包使用说明必须明确写入：
   - “ClassroomToolkit 与 PPT/WPS 权限级别一致（都管理员或都非管理员）”
   - “安全软件可能拦截全局钩子，失败会降级到消息投递”
3. 交付时附带故障指引：
   - 先看系统诊断结果
   - 再检查权限级别
   - 最后切换 WPS 兼容策略到消息模式

## 7. 无现场发布的风险标注（必须声明）

发布说明中必须附加以下声明：

- “本版本未完成课堂现场人工门（双屏/DPI/真实课件）验收。”
- “若在特定 PPT/WPS 版本出现识别偏差，请按 token 覆盖模板补充规则并回传日志。”

## 8. 回滚触发与动作

触发条件（任一满足）：

- 教室首批试用出现“崩溃/卡死/翻页完全失效”。
- 同类兼容故障在两台以上设备复现。

立即动作：

1. 停止扩散当前包；
2. 回退到上一个通过现场验收的版本；
3. 按 `docs/runbooks/migration-rollback-playbook.md` 执行回滚；
4. 补齐现场矩阵验收后再恢复发布。

