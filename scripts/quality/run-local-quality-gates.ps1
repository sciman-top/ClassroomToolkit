[CmdletBinding()]
param(
    [ValidateSet("quick", "standard", "full")]
    [string]$Profile = "standard",
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

function Resolve-PowerShellExecutable {
    if (Get-Command "pwsh" -ErrorAction SilentlyContinue) {
        return "pwsh"
    }

    if (Get-Command "powershell" -ErrorAction SilentlyContinue) {
        return "powershell"
    }

    throw "No PowerShell executable found. Expected 'pwsh' or 'powershell'."
}

function Test-ContainsOrdinalIgnoreCase {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text,
        [Parameter(Mandatory = $true)][string]$Value
    )

    return $Text.IndexOf($Value, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Invoke-NativeStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter()][string[]]$Arguments = @(),
        [Parameter()][int]$RetryCount = 0,
        [Parameter()][int]$RetryDelaySeconds = 2
    )

    $attempt = 0
    while ($true) {
        $attempt++
        Write-Host "[quality] START $Name (attempt=$attempt)"
        $output = & $FilePath @Arguments 2>&1
        if ($output) {
            $output | ForEach-Object { Write-Host $_ }
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Host "[quality] PASS  $Name"
            return
        }

        $outputText = [string]::Join([Environment]::NewLine, @($output))
        $isTransientFileLock = (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value "because it is being used by another process") `
            -or (Test-ContainsOrdinalIgnoreCase -Text $outputText -Value "已被另一进程使用")
        if ($attempt -le $RetryCount -and $isTransientFileLock) {
            Write-Warning "[quality] RETRY $Name due to transient file lock (sleep=${RetryDelaySeconds}s)"
            Start-Sleep -Seconds $RetryDelaySeconds
            continue
        }

        throw "[quality] FAIL  $Name (exit=$LASTEXITCODE)"
    }
}

Invoke-NativeStep -Name "build" -FilePath "dotnet" -Arguments @(
    "build",
    "ClassroomToolkit.sln",
    "-c",
    $Configuration,
    "-m:1"
) -RetryCount 2

$stableTestsScript = Join-Path $PSScriptRoot "..\validation\run-stable-tests.ps1"
$powerShellExe = Resolve-PowerShellExecutable
if (Test-Path -LiteralPath $stableTestsScript) {
    $stableTestArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $stableTestsScript,
        "-Configuration",
        $Configuration,
        "-Profile",
        $Profile,
        "-SkipBuild"
    )
    # standard 把稳定测试与核心契约并入同一次 dotnet test：两级过滤的并集
    # 恰为 Gate!=Performance（两组 Gate 标记互斥），省去一次测试主机启动；
    # quick/full 维持分步执行，CI 亦保持分步以便步骤级报告。
    if ($Profile -eq "standard") {
        $stableTestArgs += @("-FilterOverride", "Gate!=Performance")
    }
    Invoke-NativeStep -Name "stable-tests" -FilePath $powerShellExe -Arguments $stableTestArgs -RetryCount 1
}
else {
    Invoke-NativeStep -Name "test(full)" -FilePath "dotnet" -Arguments @(
        "test",
        "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
        "-c",
        $Configuration,
        "-m:1"
    ) -RetryCount 1
}

if ($Profile -ne "standard") {
    Invoke-NativeStep -Name "test(contract)" -FilePath "dotnet" -Arguments @(
        "test",
        "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
        "-c",
        $Configuration,
        "-m:1",
        "--no-build",
        "--filter",
        "Gate=CoreContract"
    ) -RetryCount 1
}

Invoke-NativeStep -Name "hotspot" -FilePath $powerShellExe -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "scripts/quality/check-hotspot-line-budgets.ps1"
)

if ($Profile -eq "full") {
    Invoke-NativeStep -Name "dependency-vulnerability" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/quality/check-dependency-vulnerabilities.ps1"
    )
}

if ($Profile -eq "full") {
    Invoke-NativeStep -Name "dependency-upgrade-audit" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/quality/check-dependency-upgrade-feasibility.ps1"
    )

    # The analyzer baseline is collected under the configuration declared inside
    # analyzer-backlog-baseline.json; the check script aligns to it and fails
    # loudly on mismatch, so the gate build configuration must not leak here.
    Invoke-NativeStep -Name "analyzer-latest-all" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/quality/check-analyzer-backlog-baseline.ps1"
    )
}

Write-Host "[quality] ALL PASS"
