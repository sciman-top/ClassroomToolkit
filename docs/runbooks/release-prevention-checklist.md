# 发布前预防措施清单（sciman课堂工具箱）

## 目标
- 在正式对外发布前，把运行时、兼容性、翻页/遥控稳定性、误报风险的预防动作全部前置完成。

## 一、发布产物策略
1. 标准版（Framework-Dependent, win-x64）
- 命令：
```powershell
dotnet publish src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/publish/fdd-win-x64
```

2. 离线版（Self-Contained, win-x64）
- 命令：
```powershell
dotnet publish src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/publish/sc-win-x64
```

3. 禁止项
- 不发布 x86 包。
- 不启用单文件自解压发布（误报风险高）。
- 不启用 Trim（当前互操作与反射路径较多，风险不划算）。

## 二、运行时与启动兜底
1. 标准版必须提供 `启动.bat`。
2. `启动.bat` 必须先检测 `Microsoft.WindowsDesktop.App 10.x`。
3. 缺失时，提示用户先运行 `prereq/` 中 runtime 安装包，再重启程序（默认不静默提权安装）。
4. 安装失败时，明确引导用户改用离线版。

## 三、翻页/遥控稳定性保障
1. 运行权限一致性
- ClassroomToolkit 与 PPT/WPS 必须同权限级别运行。

2. Hook 与输入降级策略
- 保持遥控/Hook能力可用。
- 默认优先消息投递模式（PostMessage）以降低安全软件敏感度。
- Hook 不可用时自动降级到消息投递模式，并给出提示。

3. 演示识别兼容
- 允许通过覆盖 token 与自动学习扩展 WPS/PPT 版本识别。

## 四、发布前验证矩阵（最低通过要求）
1. 系统与软件组合
- Win10 + WPS
- Win10 + PPT
- Win11 + WPS
- Win11 + PPT

2. 每组必测
- 前进/后退翻页
- 滚轮翻页
- 全屏进入/退出
- 前后台切换
- 双屏场景

3. 失败收敛顺序
- 权限级别检查
- 安全软件拦截检查
- 演示窗口识别日志/诊断检查

## 五、可信分发信息
1. 程序元数据
- `Authors=sciman`
- `Company=sciman逸居`
- `Product=ClassroomToolkit`

2. 交付物
- 必带 `SHA256SUMS.txt`
- 必带简明使用说明（标准版/离线版）
- 固定下载地址与版本命名

## 六、自动化执行
1. 发布准备脚本
```powershell
powershell -File scripts/release/prepare-distribution.ps1 -Version 1.0.0 -PackageMode both -EnsureLatestRuntime -ReleaseNotesSourceUrl https://github.com/<owner>/<repo>/releases/tag/v1.0.0
```
 - 说明：同一 `Version` 默认禁止覆盖；若确需覆盖，显式追加 `-AllowOverwriteVersion`。
 - 说明：`-RunDefenderScan` 作为按需参数使用（例如疑似误报排查时）；如需扫描失败即终止，可追加 `-FailOnDefenderScanError`。
 - 说明：若不传 `-ReleaseNotesSourceUrl`，脚本会尝试从 `git remote origin` 自动推导 GitHub Release 链接；非 GitHub 远端需显式传入。

2. 发布前检查脚本
```powershell
powershell -File scripts/release/preflight-check.ps1
```
- 说明：该脚本会额外执行 `win-x64` 的 FDD/SCD 发布探针，并校验关键依赖（`pdfium.dll`、`e_sqlite3.dll`、`hostfxr.dll`、`coreclr.dll`、`vcruntime140_cor3.dll`）及 runtime 主版本。

3. 归档格式建议
- 默认用 `zip`（系统原生支持，用户无需额外工具）。
- 如需更高压缩率可选 `-ArchiveFormat 7z`，前提是打包机已安装 `7z/7za`。

4. 常用参数
- `-EnsureLatestRuntime`：自动解析并下载当前频道最新 `WindowsDesktop Runtime x64` 到 `scripts/release/prereq/`，并随离线版打包。
- `-RuntimeChannel 10.0`：覆盖 `scripts/release/release-config.json` 中的 runtime 频道（默认读取配置文件）。
- `-PackageMode standard|offline|both`：控制发布产物类型；建议日常先发 `standard`，离线场景再补 `offline`。
- `-SkipZip`：仅生成目录，不生成压缩包。
- `-ReleaseNotesSourceUrl <url>`：写入 `发布说明.txt` 的下载来源地址（可选；缺省时自动推导）。
- `-AllowOverwriteVersion`：允许覆盖同版本目录（默认关闭，防止同版本反复替换二进制）。
- `-RunDefenderScan`：发布完成后对 `artifacts/release/<version>` 执行 Defender 扫描。
- `-FailOnDefenderScanError`：配合 `-RunDefenderScan` 使用，扫描异常即失败退出。

5. 清理临时产物
```powershell
# 预览将清理的 preflight 目录
powershell -File scripts/release/clean-release-artifacts.ps1

# 执行清理 preflight 目录
powershell -File scripts/release/clean-release-artifacts.ps1 -Apply

# 额外清理缓存的 runtime 安装包
powershell -File scripts/release/clean-release-artifacts.ps1 -Apply -IncludeRuntimeCache
```

## 七、备注
- 当前阶段可先完成脚本与流程固化，不立即执行正式发布。
- 正式发布时再填入目标版本号与 runtime 安装包路径。

