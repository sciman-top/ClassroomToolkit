# 发布前检查清单

适用范围：
- GitHub 版本发布
- 标准安装版（FDD + 自动更新）
- 离线安装版（SCD + 独立更新通道）
- 与发布版本同一提交的公开源码包

## 1. 工作区

- `git status` 为空
- 没有误提交的临时文件
- 没有未纳入 `.gitignore` 的生成物

## 2. 必留内容

- `tests/ClassroomToolkit.Tests/` 下的长期自动化测试代码
- `scripts/release/` 下的发布脚本、配置和 `prereq/`
- `docs/` 下的设计、验证、运行手册

## 3. 免费项目低误报策略

- 不使用付费签名/EV 证书
- 默认发布目录式多文件（`PublishSingleFile=false`），再由 Velopack 生成安装器和更新包
- 不启用裁剪（`PublishTrimmed=false`）
- 生成 `SHA256SUMS.txt` 和 `release-manifest.json`
- 产物中保留 `bootstrap-runtime.ps1` / `启动.bat`，减少课堂现场手工配置成本

## 4. 发布入口（推荐）

1. 预检：
   - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Release -Profile full`
2. 打包：
   - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/prepare-release-artifacts.ps1 -Version <版本号> -PackageMode all -Configuration Release -EnsureLatestRuntime`

## 5. 产物要求

- 标准安装版：`artifacts/release/<version>/installer/standard/`
  - `Setup.exe`、完整更新包和 `releases.*.json`
  - FDD 应用与 .NET Desktop Runtime 引导
  - Velopack channel=`standard`
- 离线安装版：`artifacts/release/<version>/installer/offline/`
  - `Setup.exe`、完整更新包和 `releases.*.json`
  - SCD 应用；功能与标准版一致
  - Velopack channel=`offline`
- 公开源码包：`ClassroomToolkit-Source-<version>.zip`
  - 由固定 Git commit 的 `git archive` 生成，不包含未跟踪的课堂数据
- 根目录：`release-manifest.json`、`user-installers-manifest.json`、`source-package-manifest.json`

## 6. GitHub 发布

- 手动触发：`.github/workflows/release-package.yml`
- 或推送 tag：`v<version>`
- 工作流会执行：
  - `preflight-check.ps1`
  - `prepare-release-artifacts.ps1`
  - 上传产物并在 tag 事件创建 Release 附件

## 7. 推荐顺序

1. 清理工作区
2. 跑 `preflight-check.ps1`
3. 跑 `prepare-distribution.ps1`
4. 核查 `SHA256SUMS.txt`、三个发布 manifest、标准/离线更新 channel 及源码 SHA
5. 触发 `release-package.yml` 或创建 tag 发布

## 8. 私用开发迁移包

- 不属于公开 Release，也不会由 CI 自动生成。
- 数据源必须显式指定，例如 `%LOCALAPPDATA%\ClassroomToolkit`：
  - `pwsh -NoProfile -File scripts/release/prepare-private-migration.ps1 -Version <版本号> -MigrationId <机器或批次标识> -SourceRoot <数据根>`
- 生成的私有包对 `data/`、`settings.ini`、`settings.json` 逐文件记录 SHA-256。恢复时先校验哈希；目标目录非空时，只有传入 `-BackupExisting` 才会移动到可恢复的同级备份。
