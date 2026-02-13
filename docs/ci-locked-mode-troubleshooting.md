# CI `--locked-mode` 失败排查手册

更新时间：2026-02-10  
适用范围：ClassroomToolkit（GitHub Actions / .NET 8）

## 1. 先判断失败类型

1. `NU1004` / lock 文件不匹配  
- 含义：依赖图变化，但 `packages.lock.json` 未同步更新。

2. 源不可达 / 认证失败  
- 含义：CI 无法访问 NuGet 源（网络、凭据、源配置问题）。

3. SDK 差异导致解析差异  
- 含义：本地与 CI 的 .NET SDK / NuGet 行为不一致。

## 2. 本地最小复现

按顺序执行：

```powershell
dotnet --info
dotnet restore ClassroomToolkit.sln --locked-mode
```

若第二步失败，再执行：

```powershell
dotnet restore ClassroomToolkit.sln --use-lock-file
```

观察是否仅 `packages.lock.json` 发生变化。

## 2.1 10 分钟快修流程

```powershell
# 进入仓库根目录后执行
dotnet restore ClassroomToolkit.sln --use-lock-file
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
git status --short
```

判定标准：
- 若仅 `packages.lock.json` 变化：可提交修复 CI。
- 若出现源码/配置额外变化：先停止提交，排查是否误改。

## 3. 常见修复路径

### 3.1 lock 漂移（最常见）

1. 本地更新 lock：

```powershell
dotnet restore ClassroomToolkit.sln --use-lock-file
```

2. 验证测试：

```powershell
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
```

3. 确认变更范围仅为 lock 文件后提交。

### 3.2 NuGet 源问题

1. 在 CI 或本地检查源：

```powershell
dotnet nuget list source
```

2. 若需私有源，补齐凭据（Secrets）与源配置。  
3. 仓库当前无 `NuGet.config` 时，源选择依赖执行环境默认配置。

### 3.3 SDK 差异问题

1. 对齐 SDK 版本（建议固定主次版本）。  
2. CI 已使用 `actions/setup-dotnet` 的 `8.0.x`；若仍漂移，建议引入 `global.json` 固定 SDK。

## 4. 快速决策表

- 仅 lock 漂移：更新并提交 lock 文件。  
- 源不稳定：先修源与凭据，再恢复 locked mode。  
- SDK 不一致：先统一 SDK，再重跑 restore。

## 4.1 提交前检查

1. `git diff` 中是否只包含预期 lock 文件。  
2. 本地 `dotnet test` 是否通过。  
3. PR 描述是否包含：失败原因、修复动作、验证结果。  

## 5. 预防建议（保守）

1. 保持“仅 CI 强制 `--locked-mode`，本地不强制”。  
2. 依赖升级 PR 中必须包含：升级原因、lock 变更、最小验证结果。  
3. 定期检查是否出现无意依赖漂移（例如每周一次）。
