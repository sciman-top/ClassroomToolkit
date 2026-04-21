# 发布前检查清单

适用范围：
- GitHub 版本发布
- 标准版打包（FDD）
- 离线版打包（SCD）

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
- 默认发布目录式多文件（`PublishSingleFile=false`）
- 不启用裁剪（`PublishTrimmed=false`）
- 生成 `SHA256SUMS.txt` 和 `release-manifest.json`
- 产物中保留 `bootstrap-runtime.ps1` / `启动.bat`，减少课堂现场手工配置成本

## 4. 发布入口（推荐）

1. 预检：
   - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Release -Profile standard`
2. 打包：
   - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/prepare-distribution.ps1 -Version <版本号> -PackageMode all -Configuration Release -EnsureLatestRuntime`

## 5. 产物要求

- 标准版目录：`artifacts/release/<version>/standard`
  - `app/`
  - `prereq/windowsdesktop-runtime-10.x-win-x64.exe`
  - `bootstrap-runtime.ps1`
  - `启动.bat`
  - `SHA256SUMS.txt`
- 离线版目录：`artifacts/release/<version>/offline`
  - `app/`
  - `启动.bat`
  - `SHA256SUMS.txt`
- 根目录：
  - `release-manifest.json`
  - 可选 zip：`ClassroomToolkit-<version>-standard.zip` / `ClassroomToolkit-<version>-offline.zip`

## 6. GitHub 发布

- 手动触发：`.github/workflows/release-package.yml`
- 或推送 tag：`v<version>`
- 工作流会执行：
  - `preflight-check.ps1`
  - `prepare-distribution.ps1`
  - 上传产物并在 tag 事件创建 Release 附件

## 7. 推荐顺序

1. 清理工作区
2. 跑 `preflight-check.ps1`
3. 跑 `prepare-distribution.ps1`
4. 核查 `SHA256SUMS.txt` 与 `release-manifest.json`
5. 触发 `release-package.yml` 或创建 tag 发布
