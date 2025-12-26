param(
    [string]$CommitMessage = "模块迁移：本地自动提交",
    [switch]$SkipCommit,
    [switch]$SkipTests
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

$sdks = dotnet --list-sdks
if ($sdks -notmatch "^8\.0") {
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
