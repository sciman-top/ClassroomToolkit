param(
    [switch]$Fix,
    [switch]$LightFix,
    [switch]$AutoFix,
    [string]$GitDir = '.git',
    [string]$BackupPath,
    [switch]$Quiet,
    [string]$ScanAllGitUnder,
    [string]$JsonReport,
    [switch]$ProbeLockFile
)

$ErrorActionPreference = 'Stop'

function Write-Status {
    param(
        [ValidateSet('INFO','PASS','WARN','FAIL')]
        [string]$Level,
        [string]$Message
    )

    if ($Quiet -and $Level -eq 'INFO') { return }

    $prefix = "[$Level]"
    switch ($Level) {
        'PASS' { Write-Host "$prefix $Message" -ForegroundColor Green }
        'WARN' { Write-Host "$prefix $Message" -ForegroundColor Yellow }
        'FAIL' { Write-Host "$prefix $Message" -ForegroundColor Red }
        default { Write-Host "$prefix $Message" -ForegroundColor Cyan }
    }
}

function Invoke-Cmd {
    param([string]$CommandLine)

    cmd /c $CommandLine | Out-Null
    return $LASTEXITCODE
}

function Resolve-SidValue {
    param([System.Security.Principal.IdentityReference]$IdentityReference)

    if (-not $IdentityReference) { return '<null>' }

    if ($IdentityReference -is [System.Security.Principal.SecurityIdentifier]) {
        return $IdentityReference.Value
    }

    try {
        return $IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
    }
    catch {
        return $IdentityReference.Value
    }
}

function Get-CurrentIdentitySnapshot {
    $name = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    return [pscustomobject]@{
        Name = $name
        Sid = $sid
        CheckedAtUtc = [DateTime]::UtcNow.ToString("o")
    }
}

function Format-SidSummaryLine {
    param([hashtable]$SidCounts)
    if ($null -eq $SidCounts -or $SidCounts.Count -eq 0) { return "none" }
    $parts = @()
    foreach ($k in ($SidCounts.Keys | Sort-Object)) {
        $parts += ("{0}={1}" -f $k, $SidCounts[$k])
    }
    return ($parts -join "; ")
}

function Test-IndexLockProbe {
    param([string]$GitTarget)

    $indexLock = Join-Path $GitTarget "index.lock"
    if (Test-Path -LiteralPath $indexLock) {
        return [pscustomobject]@{
            Ok = $false
            Message = "index.lock already exists; skip probe to avoid interfering with active git operation."
        }
    }

    try {
        New-Item -ItemType File -Path $indexLock -Force -ErrorAction Stop | Out-Null
        Remove-Item -LiteralPath $indexLock -Force -ErrorAction Stop
        return [pscustomobject]@{
            Ok = $true
            Message = "index.lock create/remove probe succeeded."
        }
    }
    catch {
        return [pscustomobject]@{
            Ok = $false
            Message = $_.Exception.Message
        }
    }
}

function Get-DenySnapshot {
    param([string]$Target)

    $items = New-Object 'System.Collections.Generic.List[System.IO.FileSystemInfo]'
    $samples = New-Object 'System.Collections.Generic.List[string]'
    $sidCounts = @{}
    $denyCount = 0

    try {
        $items.Add((Get-Item -LiteralPath $Target -Force -ErrorAction Stop))
    }
    catch {
        return [pscustomobject]@{
            DenyCount = -1
            SidCounts = @{}
            Samples = @("TARGET_UNREADABLE: $Target; $($_.Exception.Message)")
        }
    }

    try {
        foreach ($child in Get-ChildItem -LiteralPath $Target -Recurse -Force -ErrorAction SilentlyContinue) {
            $items.Add($child)
        }
    }
    catch {}

    foreach ($item in $items) {
        try {
            $acl = Get-Acl -LiteralPath $item.FullName -ErrorAction Stop
            foreach ($rule in $acl.Access) {
                if ($rule.AccessControlType -ne [System.Security.AccessControl.AccessControlType]::Deny) { continue }

                $denyCount++
                $sid = Resolve-SidValue -IdentityReference $rule.IdentityReference
                if (-not $sidCounts.ContainsKey($sid)) { $sidCounts[$sid] = 0 }
                $sidCounts[$sid]++

                if ($samples.Count -lt 10) {
                    $samples.Add(('{0} {1} {2}' -f $item.FullName, $sid, $rule.FileSystemRights))
                }
            }
        }
        catch {
            $denyCount++
            if (-not $sidCounts.ContainsKey('ACL_READ_ERROR')) { $sidCounts['ACL_READ_ERROR'] = 0 }
            $sidCounts['ACL_READ_ERROR']++
            if ($samples.Count -lt 10) {
                $samples.Add(('{0} ACL_READ_ERROR {1}' -f $item.FullName, $_.Exception.Message))
            }
        }
    }

    return [pscustomobject]@{
        DenyCount = $denyCount
        SidCounts = $sidCounts
        Samples = @($samples)
    }
}

function Remove-RootDenyRulesBySid {
    param(
        [string]$Target,
        [string]$Sid
    )

    $rootAcl = Get-Acl -LiteralPath $Target
    $rootDeny = @($rootAcl.Access | Where-Object {
        $_.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Deny -and
        (Resolve-SidValue -IdentityReference $_.IdentityReference) -eq $Sid
    })

    if ($rootDeny.Count -gt 0) {
        foreach ($rule in $rootDeny) {
            [void]$rootAcl.RemoveAccessRuleSpecific($rule)
        }
        Set-Acl -LiteralPath $Target -AclObject $rootAcl
    }
}

function Remove-DenyRulesRecursivelyBySid {
    param(
        [string]$Target,
        [string]$Sid
    )

    [void](Invoke-Cmd ('icacls "{0}" /remove:d *{1} /T /C' -f $Target, $Sid))
    Remove-RootDenyRulesBySid -Target $Target -Sid $Sid
}

function Invoke-LightRepairBySid {
    param(
        [string]$Target,
        [string[]]$Sids
    )

    foreach ($sid in $Sids) {
        if ($sid -eq 'ACL_READ_ERROR') { continue }
        Write-Status INFO "Light repair removing DENY for SID: $sid"
        Remove-DenyRulesRecursivelyBySid -Target $Target -Sid $sid
    }
}

function Repair-GitAcl {
    param(
        [string]$Target,
        [int]$Index,
        [string]$BaseBackupPath,
        [switch]$ProbeLockFile
    )

    $before = Get-DenySnapshot -Target $Target
    $beforeSids = @($before.SidCounts.Keys | Sort-Object)
    $identity = Get-CurrentIdentitySnapshot

    $report = [ordered]@{
        Target = $Target
        CheckedAtUtc = $identity.CheckedAtUtc
        Operator = $identity.Name
        OperatorSid = $identity.Sid
        BeforeDenyCount = $before.DenyCount
        BeforeDenySids = $beforeSids
        BeforeDenySidCounts = $before.SidCounts
        Fixed = $false
        BackupPath = $null
        AfterDenyCount = $before.DenyCount
        AfterDenySids = $beforeSids
        AfterDenySidCounts = $before.SidCounts
        LockProbe = [ordered]@{
            requested = [bool]$ProbeLockFile
            ok = $null
            message = ""
        }
        Success = $false
    }

    if ($before.DenyCount -eq 0) {
        Write-Status PASS ("ACL clean: {0}" -f $Target)
        if ($ProbeLockFile) {
            $probe = Test-IndexLockProbe -GitTarget $Target
            $report.LockProbe.ok = [bool]$probe.Ok
            $report.LockProbe.message = [string]$probe.Message
            if ($probe.Ok) { Write-Status PASS "index.lock probe passed." }
            else { Write-Status FAIL ("index.lock probe failed: {0}" -f $probe.Message) }
            $report.Success = [bool]$probe.Ok
            return [pscustomobject]$report
        }
        $report.Success = $true
        return [pscustomobject]$report
    }

    Write-Status WARN ("DENY found ({0}): {1}" -f $before.DenyCount, $Target)
    Write-Status INFO ("DENY SID summary: {0}" -f (Format-SidSummaryLine $before.SidCounts))
    $before.Samples | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }

    if (-not $Fix -and -not $LightFix -and -not $AutoFix) {
        Write-Status FAIL 'DENY detected. Re-run with -Fix to auto-repair.'
        return [pscustomobject]$report
    }

    if ([string]::IsNullOrWhiteSpace($BaseBackupPath)) {
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $BaseBackupPath = Join-Path $script:RepoRoot ("acl-backup-git-{0}.txt" -f $timestamp)
    }

    $backupDir = Split-Path -Parent $BaseBackupPath
    if ([string]::IsNullOrWhiteSpace($backupDir)) { $backupDir = $script:RepoRoot }
    $backupExt = [IO.Path]::GetExtension($BaseBackupPath)
    if ([string]::IsNullOrWhiteSpace($backupExt)) { $backupExt = '.txt' }
    $backupName = [IO.Path]::GetFileNameWithoutExtension($BaseBackupPath)
    $backupPath = Join-Path $backupDir ("{0}-{1}{2}" -f $backupName, $Index, $backupExt)

    Write-Status INFO "Backup ACL to: $backupPath"
    $saveCode = Invoke-Cmd ('icacls "{0}" /save "{1}" /T /C' -f $Target, $backupPath)
    if ($saveCode -ne 0) {
        Write-Status FAIL 'Failed to backup ACL.'
        $report.BackupPath = $backupPath
        return [pscustomobject]$report
    }

    $usedStrategy = 'none'
    $lightSucceeded = $false

    if ($LightFix -or $AutoFix) {
        $usedStrategy = 'light'
        Invoke-LightRepairBySid -Target $Target -Sids $beforeSids
        $lightAfter = Get-DenySnapshot -Target $Target
        if ($lightAfter.DenyCount -eq 0) {
            $lightSucceeded = $true
        }
        elseif ($LightFix -and -not $AutoFix) {
            Write-Status FAIL ("Light repair attempted but DENY still exists ({0})." -f $lightAfter.DenyCount)
        }
        else {
            Write-Status WARN ("Light repair incomplete; DENY remains ({0}), escalating to aggressive repair." -f $lightAfter.DenyCount)
        }
    }

    if (-not $lightSucceeded -and ($Fix -or $AutoFix)) {
        $usedStrategy = if ($usedStrategy -eq 'light') { 'light+aggressive' } else { 'aggressive' }

        Write-Status INFO 'Take ownership recursively (best effort)...'
        $takeownCode = Invoke-Cmd ('takeown /F "{0}" /R /D Y' -f $Target)
        if ($takeownCode -ne 0) {
            Write-Status WARN 'takeown returned non-zero; continuing.'
        }

        Write-Status INFO 'Reset ACL recursively...'
        if ((Invoke-Cmd ('icacls "{0}" /reset /T /C' -f $Target)) -ne 0) {
            Write-Status FAIL 'ACL reset failed.'
            $report.BackupPath = $backupPath
            return [pscustomobject]$report
        }

        Write-Status INFO 'Enable inheritance recursively...'
        if ((Invoke-Cmd ('icacls "{0}" /inheritance:e /T /C' -f $Target)) -ne 0) {
            Write-Status FAIL 'Enabling inheritance failed.'
            $report.BackupPath = $backupPath
            return [pscustomobject]$report
        }

        Write-Status INFO 'Grant current user full control recursively...'
        $currentPrincipal = "{0}\{1}" -f $env:USERDOMAIN, $env:USERNAME
        if ((Invoke-Cmd ('icacls "{0}" /grant:r "{1}:(OI)(CI)F" /T /C' -f $Target, $currentPrincipal)) -ne 0) {
            Write-Status FAIL 'Granting current user full control failed.'
            $report.BackupPath = $backupPath
            return [pscustomobject]$report
        }

        Invoke-LightRepairBySid -Target $Target -Sids $beforeSids
    }

    $after = Get-DenySnapshot -Target $Target
    $report.Fixed = $true
    $report.BackupPath = $backupPath
    $report.AfterDenyCount = $after.DenyCount
    $report.AfterDenySids = @($after.SidCounts.Keys | Sort-Object)
    $report.AfterDenySidCounts = $after.SidCounts
    $report.Success = ($after.DenyCount -eq 0)

    if ($report.Success -and $ProbeLockFile) {
        $probe = Test-IndexLockProbe -GitTarget $Target
        $report.LockProbe.ok = [bool]$probe.Ok
        $report.LockProbe.message = [string]$probe.Message
        if ($probe.Ok) { Write-Status PASS "index.lock probe passed." }
        else { Write-Status FAIL ("index.lock probe failed: {0}" -f $probe.Message) }
        if (-not $probe.Ok) { $report.Success = $false }
    }

    if ($report.Success) {
        Write-Status PASS ("ACL repaired: {0}" -f $Target)
        if ($usedStrategy -ne 'none') {
            Write-Status INFO ("Repair strategy: {0}" -f $usedStrategy)
        }
        Write-Status INFO ("Rollback command: icacls . /restore `"{0}`"" -f $backupPath)
        Write-Status INFO ("Operator: {0} ({1}), checked_at_utc={2}" -f $identity.Name, $identity.Sid, $identity.CheckedAtUtc)
    }
    else {
        Write-Status FAIL ("Repair attempted but DENY still exists ({0}): {1}" -f $after.DenyCount, $Target)
        $after.Samples | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }
    }

    return [pscustomobject]$report
}

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $script:RepoRoot

$targets = New-Object 'System.Collections.Generic.List[string]'
if (-not [string]::IsNullOrWhiteSpace($ScanAllGitUnder)) {
    $scanRoot = (Resolve-Path $ScanAllGitUnder).Path
    foreach ($d in Get-ChildItem -Path $scanRoot -Directory -Filter .git -Recurse -Force -ErrorAction SilentlyContinue) {
        $targets.Add($d.FullName)
    }
}
else {
    $resolvedTarget = if ([IO.Path]::IsPathRooted($GitDir)) { $GitDir } else { Join-Path $script:RepoRoot $GitDir }
    if (-not (Test-Path -LiteralPath $resolvedTarget)) {
        Write-Status FAIL "Git directory not found: $resolvedTarget"
        exit 2
    }
    $targets.Add((Resolve-Path $resolvedTarget).Path)
}

if ($targets.Count -eq 0) {
    Write-Status FAIL 'No .git directory found for the given scope.'
    exit 2
}

$results = @()
$idx = 1
foreach ($t in ($targets | Sort-Object -Unique)) {
    Write-Status INFO "Target: $t"
    $results += Repair-GitAcl -Target $t -Index $idx -BaseBackupPath $BackupPath -ProbeLockFile:$ProbeLockFile
    $idx++
}

if (-not [string]::IsNullOrWhiteSpace($JsonReport)) {
    $outPath = if ([IO.Path]::IsPathRooted($JsonReport)) { $JsonReport } else { Join-Path $script:RepoRoot $JsonReport }
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPath -Encoding utf8
    Write-Status INFO "JSON report written: $outPath"
}

$failed = @($results | Where-Object { -not $_.Success })
if ($failed.Count -eq 0) {
    exit 0
}

if ($Fix -or $LightFix -or $AutoFix) {
    exit 8
}

exit 3
$enabledFixSwitches = 0
if ($Fix) { $enabledFixSwitches++ }
if ($LightFix) { $enabledFixSwitches++ }
if ($AutoFix) { $enabledFixSwitches++ }
if ($enabledFixSwitches -gt 1) {
    Write-Status FAIL 'Use only one of -Fix, -LightFix, -AutoFix.'
    exit 2
}
