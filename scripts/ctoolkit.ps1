﻿param(
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

function Assert-Command {
    param(
        [string]$Name,
        [string]$Hint
    )
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "缺少命令: $Name。$Hint"
    }
}

function Resolve-PowerShellExecutable {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_ALLOW_WINDOWS_POWERSHELL)) {
        $legacy = Get-Command powershell -ErrorAction SilentlyContinue
        if ($legacy) { return [string]$legacy.Source }
    }

    $programFilesPwsh = if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        Join-Path $env:ProgramFiles "PowerShell\7\pwsh.exe"
    } else {
        $null
    }
    if (-not [string]::IsNullOrWhiteSpace($programFilesPwsh) -and (Test-Path -LiteralPath $programFilesPwsh)) {
        return $programFilesPwsh
    }

    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return [string]$pwsh.Source }

    $legacyFallback = Get-Command powershell -ErrorAction SilentlyContinue
    if ($legacyFallback) { return [string]$legacyFallback.Source }

    throw "缺少命令: pwsh。请安装 PowerShell 7，或显式设置 CODEX_ALLOW_WINDOWS_POWERSHELL=1 后回退到 Windows PowerShell。"
}

function Invoke-DotnetWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [int]$MaxAttempts = 3,
        [int]$RetryDelaySeconds = 2
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        & dotnet @Arguments
        if ($LASTEXITCODE -eq 0) {
            return
        }

        if ($attempt -ge $MaxAttempts) {
            throw "dotnet $($Arguments -join ' ') failed after $MaxAttempts attempts (exit=$LASTEXITCODE)."
        }

        Write-Host "dotnet $($Arguments -join ' ') 失败，$RetryDelaySeconds 秒后重试 ($attempt/$MaxAttempts)..." -ForegroundColor Yellow
        Start-Sleep -Seconds $RetryDelaySeconds
    }
}

Write-Host "==> 环境检测" -ForegroundColor Cyan
Assert-Command -Name dotnet -Hint "请安装 .NET SDK。"
Assert-Command -Name git -Hint "请安装 Git。"
$powerShellExe = Resolve-PowerShellExecutable

$hasSupportedSdk = $false
$sdks = & dotnet --list-sdks 2>$null
foreach ($sdk in $sdks) {
    $line = "$sdk".Trim()
    if ($line -match "^10\.0\." -or $line -match "^8\.0\.") {
        $hasSupportedSdk = $true
        break
    }
}
if (-not $hasSupportedSdk) {
    throw "未检测到受支持的 .NET SDK（需 10.0.x，兼容 8.0.x）。"
}

Write-Host "==> 还原依赖" -ForegroundColor Cyan
Invoke-DotnetWithRetry -Arguments @("restore")

Write-Host "==> 构建" -ForegroundColor Cyan
Invoke-DotnetWithRetry -Arguments @("build", ".\ClassroomToolkit.sln", "-c", "Debug", "-m:1")

if (-not $SkipTests) {
    Write-Host "==> 测试" -ForegroundColor Cyan
    $stableTestsScript = Join-Path $PSScriptRoot "validation/run-stable-tests.ps1"
    if (Test-Path -LiteralPath $stableTestsScript) {
        & $powerShellExe -NoProfile -ExecutionPolicy Bypass -File $stableTestsScript -Configuration Debug -SkipBuild -Profile $StableTestProfile
        if ($LASTEXITCODE -ne 0) {
            throw "稳定测试脚本执行失败，退出码: $LASTEXITCODE"
        }
    }
    else {
        Write-Host "未检测到稳定测试脚本，回退到 dotnet test。" -ForegroundColor Yellow
        Invoke-DotnetWithRetry -Arguments @(
            "test",
            ".\tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj",
            "-c",
            "Debug",
            "--no-build",
            "-m:1"
        )
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
