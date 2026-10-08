param(
    [switch]$SkipTests,
    [switch]$BrushBaseline,
    [ValidateSet("quick", "standard", "full")]
    [string]$StableTestProfile = "standard"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

function Resolve-PowerShellExecutable {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return [string]$pwsh.Source }

    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_ALLOW_WINDOWS_POWERSHELL)) {
        $legacy = Get-Command powershell -ErrorAction SilentlyContinue
        if ($legacy) { return [string]$legacy.Source }
    }

    throw "缺少命令: pwsh。请安装 PowerShell 7，或显式设置 CODEX_ALLOW_WINDOWS_POWERSHELL=1 后回退到 Windows PowerShell。"
}

if ($PSVersionTable.PSVersion.Major -lt 7 -and [string]::IsNullOrWhiteSpace($env:CODEX_ALLOW_WINDOWS_POWERSHELL)) {
    throw "请使用 PowerShell 7；仅在维护明确的 Windows PowerShell 兼容场景时设置 CODEX_ALLOW_WINDOWS_POWERSHELL=1。"
}

$powerShellExe = $null
if (-not $SkipTests -or $BrushBaseline) {
    $powerShellExe = Resolve-PowerShellExecutable
}

if ($SkipTests) {
    Write-Host "==> 构建（跳过测试）" -ForegroundColor Cyan
    & dotnet build ".\ClassroomToolkit.sln" -c Debug -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "解决方案构建失败，退出码: $LASTEXITCODE"
    }
}
else {
    Write-Host "==> 标准质量门禁" -ForegroundColor Cyan
    $qualityGateScript = Join-Path $PSScriptRoot "quality/run-local-quality-gates.ps1"
    & $powerShellExe -NoProfile -ExecutionPolicy Bypass -File $qualityGateScript -Profile $StableTestProfile -Configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw "标准质量门禁失败，退出码: $LASTEXITCODE"
    }
}

if ($BrushBaseline) {
    Write-Host "==> 画笔质量基线采集" -ForegroundColor Cyan
    $baselineScript = Join-Path $PSScriptRoot "collect-brush-quality-baseline.ps1"
    if (-not (Test-Path $baselineScript)) {
        throw "未找到基线脚本: $baselineScript"
    }

    & $powerShellExe -NoProfile -ExecutionPolicy Bypass -File $baselineScript -Configuration Debug -SkipRestore -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "画笔质量基线采集失败，退出码: $LASTEXITCODE"
    }
}

Write-Host "==> 完成" -ForegroundColor Green
