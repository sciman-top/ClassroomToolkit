param(
    [string]$CommitMessage = "模块迁移：本地自动提交",
    [switch]$SkipCommit,
    [switch]$SkipTests,
    [switch]$BrushBaseline,
    [switch]$SmokeZOrder,
    [switch]$SmokeZOrderAuto,
    [switch]$SmokeNonInteractive,
    [switch]$CheckRefactorConsistency,
    [switch]$InstallPreCommitHook
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
Assert-Command -Name powershell -Hint "请确保可调用 PowerShell。"

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
    Invoke-DotnetWithRetry -Arguments @(
        "test",
        ".\tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj",
        "-c",
        "Debug",
        "--no-build",
        "-m:1"
    )
}

if ($CheckRefactorConsistency) {
    Write-Host "==> Refactor 文档口径一致性检查/修正" -ForegroundColor Cyan
    $consistencyScript = Join-Path $PSScriptRoot "refactor/check-doc-consistency.ps1"
    if (-not (Test-Path -LiteralPath $consistencyScript)) {
        throw "未找到一致性脚本: $consistencyScript"
    }

    & powershell -ExecutionPolicy Bypass -File $consistencyScript -Fix
    if ($LASTEXITCODE -ne 0) {
        throw "文档口径一致性检查存在未自动修复问题，退出码: $LASTEXITCODE"
    }
}

if ($InstallPreCommitHook) {
    Write-Host "==> 安装 pre-commit 文档一致性 Hook" -ForegroundColor Cyan
    $installHookScript = Join-Path $PSScriptRoot "refactor/install-precommit-consistency-hook.ps1"
    if (-not (Test-Path -LiteralPath $installHookScript)) {
        throw "未找到 hook 安装脚本: $installHookScript"
    }

    & powershell -ExecutionPolicy Bypass -File $installHookScript
    if ($LASTEXITCODE -ne 0) {
        throw "安装 pre-commit hook 失败，退出码: $LASTEXITCODE"
    }
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

