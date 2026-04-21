param(
    [switch]$Fix,
    [switch]$LightFix,
    [switch]$AutoFix,
    [string]$GitDir = ".git",
    [string]$BackupPath,
    [switch]$Quiet,
    [string]$ScanAllGitUnder,
    [string]$JsonReport,
    [switch]$ProbeLockFile,
    [switch]$ProbeGitStatus,
    [switch]$AllowGitDirOutsideScanRoot
)

$ErrorActionPreference = "Stop"

function Write-Status {
    param(
        [ValidateSet("INFO", "PASS", "WARN", "FAIL")]
        [string]$Level,
        [string]$Message
    )

    if ($Quiet -and $Level -eq "INFO") { return }

    $prefix = "[$Level]"
    switch ($Level) {
        "PASS" { Write-Host "$prefix $Message" -ForegroundColor Green }
        "WARN" { Write-Host "$prefix $Message" -ForegroundColor Yellow }
        "FAIL" { Write-Host "$prefix $Message" -ForegroundColor Red }
        default { Write-Host "$prefix $Message" -ForegroundColor Cyan }
    }
}

function Invoke-ExternalCommand {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    & $Command @Arguments | Out-Null
    return $LASTEXITCODE
}

function Resolve-SidValue {
    param([System.Security.Principal.IdentityReference]$IdentityReference)

    if (-not $IdentityReference) { return "<null>" }

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

function Test-IsInternalGitMetadataPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    $normalized = $Path.Replace("/", "\")
    return ($normalized -match "\\\.git\\(modules|worktrees)(\\|$)")
}

function Resolve-GitDirFromPointerFile {
    param([string]$PointerFilePath)

    try {
        $firstLine = Get-Content -LiteralPath $PointerFilePath -TotalCount 1 -ErrorAction Stop
    }
    catch {
        return $null
    }

    if ($firstLine -notmatch "^\s*gitdir:\s*(.+)\s*$") {
        return $null
    }

    $pointerValue = $Matches[1].Trim()
    $repoRoot = Split-Path -Parent $PointerFilePath
    $candidate = if ([IO.Path]::IsPathRooted($pointerValue)) {
        $pointerValue
    }
    else {
        Join-Path $repoRoot $pointerValue
    }

    try {
        return (Resolve-Path -LiteralPath $candidate -ErrorAction Stop).Path
    }
    catch {
        return $null
    }
}

function Test-PathWithinRoot {
    param(
        [string]$RootPath,
        [string]$CandidatePath
    )

    if ([string]::IsNullOrWhiteSpace($RootPath) -or [string]::IsNullOrWhiteSpace($CandidatePath)) {
        return $false
    }

    $rootFull = [IO.Path]::GetFullPath($RootPath).TrimEnd("\")
    $candidateFull = [IO.Path]::GetFullPath($CandidatePath).TrimEnd("\")

    if ($candidateFull.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $candidateFull.StartsWith("$rootFull\", [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-GitMetadataEntriesUnderRoot {
    param([string]$Root)

    $entries = New-Object "System.Collections.Generic.List[System.IO.FileSystemInfo]"
    $pending = New-Object "System.Collections.Generic.Queue[string]"
    $pending.Enqueue($Root)

    while ($pending.Count -gt 0) {
        $current = $pending.Dequeue()
        $children = $null

        try {
            $children = Get-ChildItem -LiteralPath $current -Force -ErrorAction Stop
        }
        catch {
            continue
        }

        $gitEntries = @($children | Where-Object { $_.Name -eq ".git" })
        if ($gitEntries.Count -gt 0) {
            foreach ($gitEntry in $gitEntries) {
                $entries.Add($gitEntry)
            }
            continue
        }

        foreach ($child in $children) {
            if (-not $child.PSIsContainer) { continue }
            if (([bool]($child.Attributes -band [IO.FileAttributes]::ReparsePoint))) { continue }
            if (Test-IsInternalGitMetadataPath -Path $child.FullName) { continue }
            $pending.Enqueue($child.FullName)
        }
    }

    return @($entries)
}

function Resolve-GitTargetFromMetadataEntry {
    param([System.IO.FileSystemInfo]$Entry)

    if ($Entry.PSIsContainer) {
        $gitDir = (Resolve-Path -LiteralPath $Entry.FullName).Path
        return [pscustomobject]@{
            GitDir = $gitDir
            RepositoryRoot = Split-Path -Parent $gitDir
            SourceKind = "git_dir"
        }
    }

    $resolvedGitDir = Resolve-GitDirFromPointerFile -PointerFilePath $Entry.FullName
    if ([string]::IsNullOrWhiteSpace($resolvedGitDir)) { return $null }

    return [pscustomobject]@{
        GitDir = $resolvedGitDir
        RepositoryRoot = Split-Path -Parent $Entry.FullName
        SourceKind = "git_file"
    }
}

function Resolve-GitTargetFromInputPath {
    param(
        [string]$InputPath,
        [string]$DefaultRoot
    )

    $candidate = if ([IO.Path]::IsPathRooted($InputPath)) {
        $InputPath
    }
    else {
        Join-Path $DefaultRoot $InputPath
    }

    if (-not (Test-Path -LiteralPath $candidate)) {
        return $null
    }

    $item = Get-Item -LiteralPath $candidate -Force -ErrorAction Stop
    if ($item.PSIsContainer) {
        if ($item.Name -eq ".git") {
            $gitDir = (Resolve-Path -LiteralPath $item.FullName).Path
            return [pscustomobject]@{
                GitDir = $gitDir
                RepositoryRoot = Split-Path -Parent $gitDir
                SourceKind = "git_dir_input"
            }
        }

        $nestedGitDir = Join-Path $item.FullName ".git"
        if (Test-Path -LiteralPath $nestedGitDir -PathType Container) {
            $gitDir = (Resolve-Path -LiteralPath $nestedGitDir).Path
            return [pscustomobject]@{
                GitDir = $gitDir
                RepositoryRoot = (Resolve-Path -LiteralPath $item.FullName).Path
                SourceKind = "repo_dir_input"
            }
        }

        if (Test-Path -LiteralPath $nestedGitDir -PathType Leaf) {
            $resolvedGitDir = Resolve-GitDirFromPointerFile -PointerFilePath $nestedGitDir
            if (-not [string]::IsNullOrWhiteSpace($resolvedGitDir)) {
                return [pscustomobject]@{
                    GitDir = $resolvedGitDir
                    RepositoryRoot = (Resolve-Path -LiteralPath $item.FullName).Path
                    SourceKind = "repo_dir_input_git_file"
                }
            }
        }

        return $null
    }

    if ($item.Name -ne ".git") { return $null }

    $resolvedGitDirFromFile = Resolve-GitDirFromPointerFile -PointerFilePath $item.FullName
    if ([string]::IsNullOrWhiteSpace($resolvedGitDirFromFile)) { return $null }

    return [pscustomobject]@{
        GitDir = $resolvedGitDirFromFile
        RepositoryRoot = Split-Path -Parent $item.FullName
        SourceKind = "git_file_input"
    }
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

function Test-GitStatusProbe {
    param([string]$RepositoryRoot)

    if ([string]::IsNullOrWhiteSpace($RepositoryRoot) -or -not (Test-Path -LiteralPath $RepositoryRoot)) {
        return [pscustomobject]@{
            Ok = $false
            Message = "Repository root missing or unreadable: $RepositoryRoot"
        }
    }

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return [pscustomobject]@{
            Ok = $false
            Message = "git command not found in PATH."
        }
    }

    $revParseOutput = (& git -C $RepositoryRoot rev-parse --git-dir 2>&1) | Out-String
    if ($LASTEXITCODE -ne 0) {
        return [pscustomobject]@{
            Ok = $false
            Message = ("git rev-parse failed: {0}" -f $revParseOutput.Trim())
        }
    }

    $statusOutput = (& git -C $RepositoryRoot status --porcelain --untracked-files=no 2>&1) | Out-String
    if ($LASTEXITCODE -ne 0) {
        return [pscustomobject]@{
            Ok = $false
            Message = ("git status probe failed: {0}" -f $statusOutput.Trim())
        }
    }

    return [pscustomobject]@{
        Ok = $true
        Message = "git status probe succeeded."
    }
}

function Get-DenySnapshot {
    param([string]$Target)

    $items = New-Object "System.Collections.Generic.List[System.IO.FileSystemInfo]"
    $samples = New-Object "System.Collections.Generic.List[string]"
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
                    $samples.Add(("{0} {1} {2}" -f $item.FullName, $sid, $rule.FileSystemRights))
                }
            }
        }
        catch {
            $denyCount++
            if (-not $sidCounts.ContainsKey("ACL_READ_ERROR")) { $sidCounts["ACL_READ_ERROR"] = 0 }
            $sidCounts["ACL_READ_ERROR"]++
            if ($samples.Count -lt 10) {
                $samples.Add(("{0} ACL_READ_ERROR {1}" -f $item.FullName, $_.Exception.Message))
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

    [void](Invoke-ExternalCommand -Command "icacls" -Arguments @($Target, "/remove:d", "*$Sid", "/T", "/C"))
    Remove-RootDenyRulesBySid -Target $Target -Sid $Sid
}

function Invoke-LightRepairBySid {
    param(
        [string]$Target,
        [string[]]$Sids
    )

    foreach ($sid in $Sids) {
        if ($sid -eq "ACL_READ_ERROR") { continue }
        Write-Status INFO "Light repair removing DENY for SID: $sid"
        Remove-DenyRulesRecursivelyBySid -Target $Target -Sid $sid
    }
}

function Repair-GitAcl {
    param(
        [string]$Target,
        [string]$RepositoryRoot,
        [string]$SourceKind,
        [int]$Index,
        [string]$BaseBackupPath,
        [switch]$ProbeLockFile,
        [switch]$ProbeGitStatus
    )

    $before = Get-DenySnapshot -Target $Target
    $beforeSids = @($before.SidCounts.Keys | Sort-Object)
    $identity = Get-CurrentIdentitySnapshot
    $requestedFixMode = if ($AutoFix) { "auto" } elseif ($LightFix) { "light" } elseif ($Fix) { "aggressive" } else { "none" }

    $report = [ordered]@{
        Target = $Target
        RepositoryRoot = $RepositoryRoot
        SourceKind = $SourceKind
        CheckedAtUtc = $identity.CheckedAtUtc
        Operator = $identity.Name
        OperatorSid = $identity.Sid
        RequestedFixMode = $requestedFixMode
        RepairStrategy = "none"
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
        GitStatusProbe = [ordered]@{
            requested = [bool]$ProbeGitStatus
            ok = $null
            message = ""
        }
        Success = $false
    }

    $probePassed = $true
    if ($before.DenyCount -eq 0) {
        Write-Status PASS ("ACL clean: {0}" -f $Target)

        if ($ProbeLockFile) {
            $lockProbe = Test-IndexLockProbe -GitTarget $Target
            $report.LockProbe.ok = [bool]$lockProbe.Ok
            $report.LockProbe.message = [string]$lockProbe.Message
            if ($lockProbe.Ok) { Write-Status PASS "index.lock probe passed." }
            else {
                Write-Status FAIL ("index.lock probe failed: {0}" -f $lockProbe.Message)
                $probePassed = $false
            }
        }

        if ($ProbeGitStatus) {
            $statusProbe = Test-GitStatusProbe -RepositoryRoot $RepositoryRoot
            $report.GitStatusProbe.ok = [bool]$statusProbe.Ok
            $report.GitStatusProbe.message = [string]$statusProbe.Message
            if ($statusProbe.Ok) { Write-Status PASS "git status probe passed." }
            else {
                Write-Status FAIL ("git status probe failed: {0}" -f $statusProbe.Message)
                $probePassed = $false
            }
        }

        $report.Success = $probePassed
        return [pscustomobject]$report
    }

    Write-Status WARN ("DENY found ({0}): {1}" -f $before.DenyCount, $Target)
    Write-Status INFO ("DENY SID summary: {0}" -f (Format-SidSummaryLine $before.SidCounts))
    $before.Samples | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }

    if (-not $Fix -and -not $LightFix -and -not $AutoFix) {
        Write-Status FAIL "DENY detected. Re-run with -LightFix, -Fix, or -AutoFix."
        return [pscustomobject]$report
    }

    if ([string]::IsNullOrWhiteSpace($BaseBackupPath)) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $BaseBackupPath = Join-Path $script:RepoRoot ("acl-backup-git-{0}.txt" -f $timestamp)
    }

    $backupDir = Split-Path -Parent $BaseBackupPath
    if ([string]::IsNullOrWhiteSpace($backupDir)) { $backupDir = $script:RepoRoot }
    if (-not (Test-Path -LiteralPath $backupDir)) {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    }

    $backupExt = [IO.Path]::GetExtension($BaseBackupPath)
    if ([string]::IsNullOrWhiteSpace($backupExt)) { $backupExt = ".txt" }
    $backupName = [IO.Path]::GetFileNameWithoutExtension($BaseBackupPath)
    $backupPath = Join-Path $backupDir ("{0}-{1}{2}" -f $backupName, $Index, $backupExt)

    Write-Status INFO "Backup ACL to: $backupPath"
    $saveCode = Invoke-ExternalCommand -Command "icacls" -Arguments @($Target, "/save", $backupPath, "/T", "/C")
    if ($saveCode -ne 0) {
        Write-Status FAIL "Failed to backup ACL."
        $report.BackupPath = $backupPath
        return [pscustomobject]$report
    }

    $usedStrategy = "none"
    $lightSucceeded = $false

    if ($LightFix -or $AutoFix) {
        $usedStrategy = "light"
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
        $usedStrategy = if ($usedStrategy -eq "light") { "light+aggressive" } else { "aggressive" }

        Write-Status INFO "Take ownership recursively (best effort)..."
        $takeownCode = Invoke-ExternalCommand -Command "takeown" -Arguments @("/F", $Target, "/R", "/D", "Y")
        if ($takeownCode -ne 0) {
            Write-Status WARN "takeown returned non-zero; continuing."
        }

        Write-Status INFO "Reset ACL recursively..."
        if ((Invoke-ExternalCommand -Command "icacls" -Arguments @($Target, "/reset", "/T", "/C")) -ne 0) {
            Write-Status FAIL "ACL reset failed."
            $report.BackupPath = $backupPath
            return [pscustomobject]$report
        }

        Write-Status INFO "Enable inheritance recursively..."
        if ((Invoke-ExternalCommand -Command "icacls" -Arguments @($Target, "/inheritance:e", "/T", "/C")) -ne 0) {
            Write-Status FAIL "Enabling inheritance failed."
            $report.BackupPath = $backupPath
            return [pscustomobject]$report
        }

        Write-Status INFO "Grant current user full control recursively..."
        $currentPrincipal = "{0}\{1}" -f $env:USERDOMAIN, $env:USERNAME
        if ((Invoke-ExternalCommand -Command "icacls" -Arguments @($Target, "/grant:r", "${currentPrincipal}:(OI)(CI)F", "/T", "/C")) -ne 0) {
            Write-Status FAIL "Granting current user full control failed."
            $report.BackupPath = $backupPath
            return [pscustomobject]$report
        }

        Invoke-LightRepairBySid -Target $Target -Sids $beforeSids
    }

    $after = Get-DenySnapshot -Target $Target
    $report.Fixed = $true
    $report.RepairStrategy = $usedStrategy
    $report.BackupPath = $backupPath
    $report.AfterDenyCount = $after.DenyCount
    $report.AfterDenySids = @($after.SidCounts.Keys | Sort-Object)
    $report.AfterDenySidCounts = $after.SidCounts
    $report.Success = ($after.DenyCount -eq 0)

    if ($report.Success -and $ProbeLockFile) {
        $lockProbe = Test-IndexLockProbe -GitTarget $Target
        $report.LockProbe.ok = [bool]$lockProbe.Ok
        $report.LockProbe.message = [string]$lockProbe.Message
        if ($lockProbe.Ok) { Write-Status PASS "index.lock probe passed." }
        else {
            Write-Status FAIL ("index.lock probe failed: {0}" -f $lockProbe.Message)
            $report.Success = $false
        }
    }

    if ($report.Success -and $ProbeGitStatus) {
        $statusProbe = Test-GitStatusProbe -RepositoryRoot $RepositoryRoot
        $report.GitStatusProbe.ok = [bool]$statusProbe.Ok
        $report.GitStatusProbe.message = [string]$statusProbe.Message
        if ($statusProbe.Ok) { Write-Status PASS "git status probe passed." }
        else {
            Write-Status FAIL ("git status probe failed: {0}" -f $statusProbe.Message)
            $report.Success = $false
        }
    }

    if ($report.Success) {
        Write-Status PASS ("ACL repaired: {0}" -f $Target)
        if ($usedStrategy -ne "none") {
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

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $script:RepoRoot

$enabledFixSwitches = 0
if ($Fix) { $enabledFixSwitches++ }
if ($LightFix) { $enabledFixSwitches++ }
if ($AutoFix) { $enabledFixSwitches++ }
if ($enabledFixSwitches -gt 1) {
    Write-Status FAIL "Use only one of -Fix, -LightFix, -AutoFix."
    exit 2
}

$targets = New-Object "System.Collections.Generic.List[object]"
if (-not [string]::IsNullOrWhiteSpace($ScanAllGitUnder)) {
    try {
        $scanRoot = (Resolve-Path -LiteralPath $ScanAllGitUnder -ErrorAction Stop).Path
    }
    catch {
        Write-Status FAIL "Scan root not found: $ScanAllGitUnder"
        exit 2
    }

    Write-Status INFO "Scanning for repositories under: $scanRoot"
    $entries = Get-GitMetadataEntriesUnderRoot -Root $scanRoot
    foreach ($entry in $entries) {
        if (Test-IsInternalGitMetadataPath -Path $entry.FullName) { continue }
        $resolved = Resolve-GitTargetFromMetadataEntry -Entry $entry
        if ($null -eq $resolved) { continue }

        if (-not $AllowGitDirOutsideScanRoot -and -not (Test-PathWithinRoot -RootPath $scanRoot -CandidatePath $resolved.GitDir)) {
            Write-Status WARN ("Skip target outside scan root: gitdir={0}, repo={1}" -f $resolved.GitDir, $resolved.RepositoryRoot)
            continue
        }

        $targets.Add($resolved)
    }
}
else {
    $resolvedTarget = Resolve-GitTargetFromInputPath -InputPath $GitDir -DefaultRoot $script:RepoRoot
    if ($null -eq $resolvedTarget) {
        Write-Status FAIL "Git directory/repository not found or unsupported input: $GitDir"
        exit 2
    }
    $targets.Add($resolvedTarget)
}

$uniqueTargets = @(
    $targets |
    Group-Object GitDir |
    ForEach-Object { $_.Group[0] } |
    Sort-Object GitDir
)

if ($uniqueTargets.Count -eq 0) {
    Write-Status FAIL "No .git metadata target found for the given scope."
    exit 2
}

$results = @()
$idx = 1
foreach ($target in $uniqueTargets) {
    Write-Status INFO ("Target: {0} (repo={1}, source={2})" -f $target.GitDir, $target.RepositoryRoot, $target.SourceKind)
    $results += Repair-GitAcl `
        -Target $target.GitDir `
        -RepositoryRoot $target.RepositoryRoot `
        -SourceKind $target.SourceKind `
        -Index $idx `
        -BaseBackupPath $BackupPath `
        -ProbeLockFile:$ProbeLockFile `
        -ProbeGitStatus:$ProbeGitStatus
    $idx++
}

if (-not [string]::IsNullOrWhiteSpace($JsonReport)) {
    $outPath = if ([IO.Path]::IsPathRooted($JsonReport)) { $JsonReport } else { Join-Path $script:RepoRoot $JsonReport }
    $outDir = Split-Path -Parent $outPath
    if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }
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
