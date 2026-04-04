param(
    [ValidateSet("start", "status", "stop", "cleanup")]
    [string]$Action = "start",
    [string]$Name = "default",
    [string]$Url = "about:blank",
    [ValidateRange(1, 65535)]
    [int]$Port = 9222,
    [ValidateSet("auto", "chrome", "edge")]
    [string]$Browser = "auto",
    [string]$ProfileRoot = $(Join-Path $env:LOCALAPPDATA "BrowserSessions"),
    [switch]$AttachOnly,
    [switch]$AllowExtensions,
    [switch]$StrictStatus
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-BrowserPath {
    param([string]$BrowserHint)

    $chromeCandidates = @(
        (Join-Path ${env:ProgramFiles} "Google\Chrome\Application\chrome.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Google\Chrome\Application\chrome.exe"),
        (Join-Path $env:LocalAppData "Google\Chrome\Application\chrome.exe")
    )
    $edgeCandidates = @(
        (Join-Path ${env:ProgramFiles} "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path $env:LocalAppData "Microsoft\Edge\Application\msedge.exe")
    )

    $candidates = @()
    if ($BrowserHint -eq "chrome") {
        $candidates = $chromeCandidates
    } elseif ($BrowserHint -eq "edge") {
        $candidates = $edgeCandidates
    } else {
        $candidates = @($chromeCandidates + $edgeCandidates)
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    $commands = if ($BrowserHint -eq "chrome") {
        @("chrome.exe", "google-chrome.exe")
    } elseif ($BrowserHint -eq "edge") {
        @("msedge.exe")
    } else {
        @("chrome.exe", "msedge.exe", "google-chrome.exe")
    }

    foreach ($command in $commands) {
        $resolved = Get-Command $command -ErrorAction SilentlyContinue
        if ($null -ne $resolved -and -not [string]::IsNullOrWhiteSpace($resolved.Source)) {
            return [System.IO.Path]::GetFullPath($resolved.Source)
        }
    }

    throw "Cannot locate the requested browser ($BrowserHint). Install Chrome/Edge first."
}

function Get-SessionPaths {
    param([string]$SessionName, [string]$Root)

    $profilePath = Join-Path $Root $SessionName
    $metaRoot = Join-Path $Root ".meta"
    $metaPath = Join-Path $metaRoot ($SessionName + ".json")
    return [pscustomobject]@{
        profile = $profilePath
        meta_root = $metaRoot
        meta = $metaPath
    }
}

function Read-SessionMetadata {
    param([string]$MetaPath)
    if (-not (Test-Path -LiteralPath $MetaPath)) {
        return $null
    }
    try {
        return (Get-Content -LiteralPath $MetaPath -Raw | ConvertFrom-Json)
    } catch {
        return $null
    }
}

function Write-SessionMetadata {
    param([string]$MetaPath, [psobject]$Metadata)
    $metaDir = Split-Path -Parent $MetaPath
    New-Item -ItemType Directory -Force -Path $metaDir | Out-Null
    ($Metadata | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $MetaPath -Encoding UTF8
}

function Get-PortOwner {
    param([int]$TargetPort)

    $ownerPid = $null
    try {
        $conn = Get-NetTCPConnection -State Listen -LocalPort $TargetPort -ErrorAction Stop | Select-Object -First 1
        if ($null -ne $conn) {
            $ownerPid = [int]$conn.OwningProcess
        }
    } catch {
        $ownerPid = $null
    }

    if ($null -eq $ownerPid) {
        $netstat = & netstat -ano -p tcp 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($netstat)) {
            foreach ($line in @($netstat -split "`r?`n")) {
                $normalized = (($line -replace '^\s+', '') -replace '\s+', ' ')
                if (-not $normalized.StartsWith("TCP ")) { continue }
                if ($normalized -notmatch "LISTENING\s+\d+$") { continue }
                $parts = $normalized.Split(" ")
                if ($parts.Count -lt 5) { continue }
                $localEndpoint = $parts[1]
                if ($localEndpoint -match ":(\d+)$") {
                    $localPort = [int]$Matches[1]
                    if ($localPort -eq $TargetPort) {
                        $ownerPid = [int]$parts[4]
                        break
                    }
                }
            }
        }
    }

    if ($null -eq $ownerPid) {
        return $null
    }

    try {
        $proc = Get-Process -Id $ownerPid -ErrorAction Stop
        return [pscustomobject]@{
            pid = $ownerPid
            process_name = [string]$proc.ProcessName
            process_path = [string]$proc.Path
        }
    } catch {
        return [pscustomobject]@{
            pid = $ownerPid
            process_name = ""
            process_path = ""
        }
    }
}

function Invoke-CdpProbe {
    param([int]$TargetPort, [int]$TimeoutSeconds = 2)

    $url = "http://127.0.0.1:$TargetPort/json/version"
    try {
        $resp = Invoke-RestMethod -Method Get -Uri $url -TimeoutSec $TimeoutSeconds
        if ($null -eq $resp) {
            return [pscustomobject]@{ ok = $false; browser = ""; websocket = ""; endpoint = $url }
        }
        return [pscustomobject]@{
            ok = -not [string]::IsNullOrWhiteSpace([string]$resp.webSocketDebuggerUrl)
            browser = [string]$resp.Browser
            websocket = [string]$resp.webSocketDebuggerUrl
            endpoint = $url
        }
    } catch {
        return [pscustomobject]@{ ok = $false; browser = ""; websocket = ""; endpoint = $url }
    }
}

function Get-SessionStatus {
    param([int]$TargetPort)
    $owner = Get-PortOwner -TargetPort $TargetPort
    $listening = $null -ne $owner
    $cdp = Invoke-CdpProbe -TargetPort $TargetPort
    return [pscustomobject]@{
        listening = $listening
        owner = $owner
        cdp = $cdp
    }
}

function Get-AttachCommand {
    param([int]$TargetPort, [string]$TargetUrl)
    return "agent-browser --cdp $TargetPort open $TargetUrl"
}

function Wait-CdpReady {
    param([int]$TargetPort, [int]$TimeoutSeconds = 12)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $status = Get-SessionStatus -TargetPort $TargetPort
        if ($status.listening -and $status.cdp.ok) {
            return $status
        }
        Start-Sleep -Milliseconds 400
    }
    return (Get-SessionStatus -TargetPort $TargetPort)
}

function Write-StatusLines {
    param(
        [psobject]$Status,
        [psobject]$Metadata,
        [string]$SessionName,
        [string]$ProfilePath,
        [string]$MetaPath,
        [int]$TargetPort,
        [string]$TargetUrl
    )

    Write-Host "Action:  status"
    Write-Host "Name:    $SessionName"
    Write-Host "Profile: $ProfilePath"
    Write-Host "Meta:    $MetaPath"
    Write-Host "Port:    $TargetPort"
    Write-Host "Url:     $TargetUrl"

    if ($null -ne $Metadata) {
        Write-Host "MetaPid: $($Metadata.pid)"
        Write-Host "MetaAt:  $($Metadata.started_at)"
    } else {
        Write-Host "Meta:    missing"
    }

    if ($Status.listening) {
        $ownerText = if ($null -ne $Status.owner) { "$($Status.owner.process_name) (pid=$($Status.owner.pid))" } else { "unknown" }
        Write-Host "Listen:  yes ($ownerText)"
    } else {
        Write-Host "Listen:  no"
    }

    if ($Status.cdp.ok) {
        Write-Host "CDP:     ready ($($Status.cdp.browser))"
        Write-Host "Attach:  $(Get-AttachCommand -TargetPort $TargetPort -TargetUrl $TargetUrl)"
    } else {
        Write-Host "CDP:     not ready ($($Status.cdp.endpoint))"
    }
}

function Stop-SessionProcess {
    param([int[]]$CandidatePids)
    $stopped = 0
    foreach ($candidatePid in @($CandidatePids | Select-Object -Unique)) {
        if ($candidatePid -le 0) { continue }
        try {
            $proc = Get-Process -Id $candidatePid -ErrorAction Stop
            if (@("chrome", "msedge") -notcontains $proc.ProcessName.ToLowerInvariant()) {
                Write-Host "[SKIP] pid=$candidatePid process=$($proc.ProcessName) is not chrome/msedge"
                continue
            }
            Stop-Process -Id $candidatePid -ErrorAction Stop
            Write-Host "[STOP] pid=$candidatePid process=$($proc.ProcessName)"
            $stopped++
        } catch {
            if ($_.Exception.Message -like "*Cannot find a process*") {
                Write-Host "[SKIP] pid=$candidatePid already exited"
            } else {
                Write-Host "[WARN] failed to stop pid=$candidatePid : $($_.Exception.Message)"
            }
        }
    }
    return $stopped
}

$paths = Get-SessionPaths -SessionName $Name -Root $ProfileRoot
$profilePath = [System.IO.Path]::GetFullPath($paths.profile)
$metaPath = [System.IO.Path]::GetFullPath($paths.meta)
$attachCommand = Get-AttachCommand -TargetPort $Port -TargetUrl $Url

if ($Action -eq "status") {
    $metadata = Read-SessionMetadata -MetaPath $metaPath
    $status = Get-SessionStatus -TargetPort $Port
    Write-StatusLines -Status $status -Metadata $metadata -SessionName $Name -ProfilePath $profilePath -MetaPath $metaPath -TargetPort $Port -TargetUrl $Url
    if ($StrictStatus -and -not ($status.listening -and $status.cdp.ok)) {
        exit 1
    }
    exit 0
}

if ($Action -eq "stop") {
    $metadata = Read-SessionMetadata -MetaPath $metaPath
    $status = Get-SessionStatus -TargetPort $Port
    $candidatePids = @()
    if ($null -ne $metadata -and $metadata.PSObject.Properties.Name -contains "pid") {
        $candidatePids += [int]$metadata.pid
    }
    if ($status.listening -and $null -ne $status.owner) {
        $candidatePids += [int]$status.owner.pid
    }
    $stopped = Stop-SessionProcess -CandidatePids $candidatePids
    Start-Sleep -Milliseconds 500
    $statusAfter = Get-SessionStatus -TargetPort $Port
    Write-Host "Stopped: $stopped"
    Write-Host "ListenAfter: $($statusAfter.listening)"
    if ($statusAfter.cdp.ok) {
        Write-Host "[WARN] CDP still reachable after stop."
        exit 1
    }
    exit 0
}

if ($Action -eq "cleanup") {
    $metadata = Read-SessionMetadata -MetaPath $metaPath
    $status = Get-SessionStatus -TargetPort $Port
    $candidatePids = @()
    if ($null -ne $metadata -and $metadata.PSObject.Properties.Name -contains "pid") {
        $candidatePids += [int]$metadata.pid
    }
    if ($status.listening -and $null -ne $status.owner) {
        $candidatePids += [int]$status.owner.pid
    }
    $null = Stop-SessionProcess -CandidatePids $candidatePids

    $rootFull = [System.IO.Path]::GetFullPath($ProfileRoot).TrimEnd('\') + '\'
    $profileFull = $profilePath.TrimEnd('\') + '\'
    if (-not $profileFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refuse cleanup outside profile root. profile=$profilePath root=$ProfileRoot"
    }

    if (Test-Path -LiteralPath $profilePath) {
        Remove-Item -LiteralPath $profilePath -Recurse -Force
        Write-Host "[REMOVED] profile=$profilePath"
    } else {
        Write-Host "[SKIP] profile not found: $profilePath"
    }
    if (Test-Path -LiteralPath $metaPath) {
        Remove-Item -LiteralPath $metaPath -Force
        Write-Host "[REMOVED] metadata=$metaPath"
    }
    exit 0
}

New-Item -ItemType Directory -Force -Path $profilePath | Out-Null
New-Item -ItemType Directory -Force -Path $paths.meta_root | Out-Null

Write-Host "Action:  start"
Write-Host "Name:    $Name"
Write-Host "Profile: $profilePath"
Write-Host "Meta:    $metaPath"
Write-Host "Port:    $Port"
Write-Host "Url:     $Url"

$status = Get-SessionStatus -TargetPort $Port
if ($status.listening) {
    $ownerText = if ($null -ne $status.owner) { "$($status.owner.process_name) (pid=$($status.owner.pid))" } else { "unknown" }
    if ($status.cdp.ok) {
        Write-Host "Port $Port already serves CDP. Reuse existing browser: $ownerText"
        Write-Host "Attach:  $attachCommand"
        exit 0
    }

    Write-Host "[ERROR] Port $Port is occupied by $ownerText, but CDP handshake failed."
    Write-Host "Choose another port or stop the process first."
    exit 1
}

if ($AttachOnly) {
    Write-Host "AttachOnly is set, but port $Port is not listening."
    Write-Host "Attach:  $attachCommand"
    exit 1
}

$browserPath = Resolve-BrowserPath -BrowserHint $Browser
$arguments = @(
    "--remote-debugging-port=$Port",
    "--user-data-dir=$profilePath",
    "--no-first-run",
    "--no-default-browser-check",
    "--disable-default-apps",
    "--disable-sync",
    "--new-window",
    $Url
)
if (-not $AllowExtensions) {
    $arguments += @(
        "--disable-extensions",
        "--disable-component-extensions-with-background-pages"
    )
}

Write-Host "Launching: $browserPath"
$process = Start-Process -FilePath $browserPath -ArgumentList $arguments -PassThru

$ready = Wait-CdpReady -TargetPort $Port
if (-not ($ready.listening -and $ready.cdp.ok)) {
    Write-Host "[WARN] Browser launched (pid=$($process.Id)) but CDP is not ready yet."
    Write-Host "Probe:  $($ready.cdp.endpoint)"
}

$metadata = [pscustomobject]@{
    name = $Name
    port = $Port
    url = $Url
    profile_path = $profilePath
    browser_path = $browserPath
    pid = $process.Id
    started_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    cdp_ready = [bool]($ready.listening -and $ready.cdp.ok)
}
Write-SessionMetadata -MetaPath $metaPath -Metadata $metadata

Write-Host ""
Write-Host "Attach: $attachCommand"
Write-Host "Status: powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Action status -Name $Name -Port $Port -Url $Url"
Write-Host "Stop:   powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Action stop -Name $Name -Port $Port"
Write-Host "Cleanup:powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Action cleanup -Name $Name -Port $Port"
