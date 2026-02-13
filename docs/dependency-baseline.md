# 依赖与还原源基线（保守方案）

更新时间：2026-02-10
范围：ClassroomToolkit 仓库（仅文档基线，不改变构建行为）

## 1. 当前依赖事实（仓库证据）

- 应用层 `src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`
  - `PdfiumViewer.Core` `1.0.4`
  - `PdfiumViewer.Native.x86_64.no_v8-no_xfa` `2018.4.8.256`
  - `System.Speech` `8.0.0`
- 基础设施层 `src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj`
  - `ClosedXML` `0.102.3`
- 测试层 `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
  - `ClosedXML` `0.102.3`
  - `FluentAssertions` `6.12.0`
  - `Microsoft.NET.Test.Sdk` `17.6.0`
  - `xunit` `2.4.2`
  - `xunit.runner.visualstudio` `2.4.5`
  - `coverlet.collector` `6.0.0`

## 2. 源配置现状（仓库内）

- 仓库根未发现 `NuGet.config`。
- 已存在 lock 文件：
  - `src/ClassroomToolkit.App/packages.lock.json`
  - `src/ClassroomToolkit.Domain/packages.lock.json`
  - `src/ClassroomToolkit.Infra/packages.lock.json`
  - `src/ClassroomToolkit.Interop/packages.lock.json`
  - `src/ClassroomToolkit.Services/packages.lock.json`
  - `tests/ClassroomToolkit.Tests/packages.lock.json`
- 结论：依赖版本已锁定；还原源选择仍受本机/CI 环境默认 NuGet 配置影响。

## 3. 还原与审计基线（当前执行口径）

- CI 使用 `dotnet restore --locked-mode`（见 `.github/workflows/locked-restore.yml`）。
- 本地开发默认不强制 locked mode（保守策略）。
- 建议每次升级依赖时记录：
  - 变更前后包名与版本
  - 触发原因（安全修复/功能需要）
  - 最小验证命令与结果

## 4. 快速核验命令

```powershell
# 1) 检查 lock 文件是否齐全
rg --files -g "**/packages.lock.json"

# 2) 校验锁定还原
dotnet restore ClassroomToolkit.sln --locked-mode

# 3) 最小测试验证
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
```

## 5. 当前落地策略（已执行）

- 已新增 CI 工作流：`.github/workflows/locked-restore.yml`
- 策略：仅 CI 使用 `dotnet restore --locked-mode`；本地开发不强制 locked mode。
- 目的：在不改变本地工作流的前提下，约束主分支依赖漂移。

## 6. 回滚说明

- 文档回滚：删除或回退本文件。
- CI 回滚：删除或回退 `.github/workflows/locked-restore.yml`。
