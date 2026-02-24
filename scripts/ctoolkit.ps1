param(
    [string]$CommitMessage = "模块迁移：本地自动提交",
    [switch]$SkipCommit,
    [switch]$SkipTests,
    [switch]$BrushBaseline,
    [switch]$SmokeZOrder,
    [switch]$SmokeZOrderAuto,
    [switch]$SmokeNonInteractive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Command {
    param(
        [string]$Name,
        [string]$Hint
    )
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "缺少命令: $Name。$Hint"
    }
}

Write-Host "==> 环境检测" -ForegroundColor Cyan
Assert-Command -Name dotnet -Hint "请安装 .NET 8 SDK。"
Assert-Command -Name git -Hint "请安装 Git。"
Assert-Command -Name powershell -Hint "请确保可调用 PowerShell。"

$hasNet8 = $false
$sdks = & dotnet --list-sdks 2>$null
foreach ($sdk in $sdks) {
    $line = "$sdk".Trim()
    if ($line -match "^8\.0\.") {
        $hasNet8 = $true
        break
    }
}
if (-not $hasNet8) {
    throw "未检测到 .NET 8 SDK。"
}

Write-Host "==> 还原依赖" -ForegroundColor Cyan
dotnet restore

Write-Host "==> 构建" -ForegroundColor Cyan
dotnet build .\ClassroomToolkit.sln -c Debug

if (-not $SkipTests) {
    Write-Host "==> 测试" -ForegroundColor Cyan
    dotnet test .\tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug
}

if ($BrushBaseline) {
    Write-Host "==> 画笔质量基线采集" -ForegroundColor Cyan
    $baselineScript = Join-Path $PSScriptRoot "collect-brush-quality-baseline.ps1"
    if (-not (Test-Path $baselineScript)) {
        throw "未找到基线脚本: $baselineScript"
    }

    & powershell -ExecutionPolicy Bypass -File $baselineScript -Configuration Debug -SkipRestore -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "画笔质量基线采集失败，退出码: $LASTEXITCODE"
    }
}

if ($SmokeZOrder) {
    Write-Host "==> Z-Order 冒烟" -ForegroundColor Cyan
    $smokeScript = Join-Path $PSScriptRoot "smoke-zorder.ps1"
    if (-not (Test-Path $smokeScript)) {
        throw "未找到冒烟脚本: $smokeScript"
    }

    $smokeArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", $smokeScript,
        "-SkipBuild",
        "-SkipTests"
    )

    if ($SmokeNonInteractive) {
        $smokeArgs += "-NonInteractive"
    }

    & powershell @smokeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Z-Order 冒烟脚本执行失败，退出码: $LASTEXITCODE"
    }
}

if ($SmokeZOrderAuto) {
    Write-Host "==> Z-Order 自动冒烟" -ForegroundColor Cyan
    $smokeScript = Join-Path $PSScriptRoot "smoke-zorder-auto.ps1"
    if (-not (Test-Path $smokeScript)) {
        throw "未找到自动冒烟脚本: $smokeScript"
    }

    $smokeArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", $smokeScript,
        "-SkipBuild",
        "-SkipTests"
    )

    & powershell @smokeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Z-Order 自动冒烟脚本执行失败，退出码: $LASTEXITCODE"
    }
}

if (-not $SkipCommit) {
    Write-Host "==> 自动提交" -ForegroundColor Cyan
    $status = git status --porcelain
    if ([string]::IsNullOrWhiteSpace($status)) {
        Write-Host "没有变更需要提交。" -ForegroundColor Yellow
    }
    else {
        git add -A
        git commit -m $CommitMessage
    }
}

Write-Host "==> 完成" -ForegroundColor Green

