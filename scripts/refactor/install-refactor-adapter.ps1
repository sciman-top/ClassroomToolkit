param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRepoRoot,
    [string]$SourceRepoRoot = ".",
    [string]$ManifestPath = "scripts/refactor/refactor-adapter.manifest.json",
    [string]$Mode = "",
    [switch]$SkipTaskGraph,
    [switch]$CopyStateFromSource,
    [string]$ProjectName = "",
    [switch]$Force,
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [string]$Path,
        [string]$BasePath = ""
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    $combined = Join-Path $BasePath $Path
    return (Resolve-Path -LiteralPath $combined).Path
}

function Test-SameFileContent {
    param(
        [string]$SourcePath,
        [string]$TargetPath
    )

    if (-not (Test-Path -LiteralPath $SourcePath) -or -not (Test-Path -LiteralPath $TargetPath)) {
        return $false
    }

    $sourceHash = (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $TargetPath -Algorithm SHA256).Hash
    return $sourceHash -eq $targetHash
}

function Ensure-ParentDirectory {
    param([string]$Path)

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
}

function Copy-ManagedFile {
    param(
        [string]$SourcePath,
        [string]$TargetPath,
        [switch]$AllowOverwrite
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return [pscustomobject]@{
            action = "missing_source"
            source = $SourcePath
            target = $TargetPath
        }
    }

    if (Test-Path -LiteralPath $TargetPath) {
        if (Test-SameFileContent -SourcePath $SourcePath -TargetPath $TargetPath) {
            return [pscustomobject]@{
                action = "unchanged"
                source = $SourcePath
                target = $TargetPath
            }
        }

        if (-not $AllowOverwrite) {
            return [pscustomobject]@{
                action = "conflict"
                source = $SourcePath
                target = $TargetPath
            }
        }
    }

    Ensure-ParentDirectory -Path $TargetPath
    Copy-Item -LiteralPath $SourcePath -Destination $TargetPath -Force

    return [pscustomobject]@{
        action = if (Test-Path -LiteralPath $TargetPath) { "copied" } else { "failed" }
        source = $SourcePath
        target = $TargetPath
    }
}

$sourceRoot = Resolve-AbsolutePath -Path $SourceRepoRoot

if (-not (Test-Path -LiteralPath $TargetRepoRoot)) {
    New-Item -ItemType Directory -Path $TargetRepoRoot -Force | Out-Null
}

$targetRoot = Resolve-AbsolutePath -Path $TargetRepoRoot

$manifestAbsolutePath = if ([System.IO.Path]::IsPathRooted($ManifestPath)) {
    Resolve-AbsolutePath -Path $ManifestPath
}
else {
    Resolve-AbsolutePath -Path $ManifestPath -BasePath $sourceRoot
}

$manifest = Get-Content -LiteralPath $manifestAbsolutePath -Raw | ConvertFrom-Json

$copied = [System.Collections.Generic.List[object]]::new()
$unchanged = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[object]]::new()
$conflicts = [System.Collections.Generic.List[object]]::new()
$errors = [System.Collections.Generic.List[object]]::new()

function Test-EntryModeMatch {
    param(
        [object]$Entry,
        [string]$RequestedMode
    )

    if ([string]::IsNullOrWhiteSpace($RequestedMode)) {
        return $true
    }

    if ($null -eq $Entry.PSObject.Properties["mode_id"] -or [string]::IsNullOrWhiteSpace([string]$Entry.mode_id)) {
        return $true
    }

    return [string]$Entry.mode_id -eq $RequestedMode
}

foreach ($entry in @($manifest.entries)) {
    if ($null -eq $entry) {
        continue
    }

    $role = ""
    if ($null -ne $entry.PSObject.Properties["role"]) {
        $role = [string]$entry.role
    }

    if ($SkipTaskGraph -and $role -eq "task-graph") {
        $skipped.Add([pscustomobject]@{
                path = [string]$entry.path
                reason = "skip_task_graph"
            }) | Out-Null
        continue
    }

    $kind = [string]$entry.kind
    $relativePath = [string]$entry.path
    $required = $false
    if ($null -ne $entry.PSObject.Properties["required"]) {
        $required = [bool]$entry.required
    }

    if (-not (Test-EntryModeMatch -Entry $entry -RequestedMode $Mode)) {
        $skipped.Add([pscustomobject]@{
                path = [string]$entry.path
                reason = "mode_filter"
                mode = $Mode
            }) | Out-Null
        continue
    }

    $sourcePath = Join-Path $sourceRoot $relativePath
    $targetPath = Join-Path $targetRoot $relativePath

    if ($kind -eq "file") {
        $result = Copy-ManagedFile -SourcePath $sourcePath -TargetPath $targetPath -AllowOverwrite:$Force
        switch ([string]$result.action) {
            "copied" { $copied.Add($result) | Out-Null }
            "unchanged" { $unchanged.Add($result) | Out-Null }
            "conflict" { $conflicts.Add($result) | Out-Null }
            default {
                if ($required) {
                    $errors.Add($result) | Out-Null
                }
                else {
                    $skipped.Add($result) | Out-Null
                }
            }
        }
        continue
    }

    if ($kind -eq "seed-state") {
        if ($CopyStateFromSource) {
            $result = Copy-ManagedFile -SourcePath $sourcePath -TargetPath $targetPath -AllowOverwrite:$Force
            switch ([string]$result.action) {
                "copied" { $copied.Add($result) | Out-Null }
                "unchanged" { $unchanged.Add($result) | Out-Null }
                "conflict" { $conflicts.Add($result) | Out-Null }
                default {
                    if ($required) {
                        $errors.Add($result) | Out-Null
                    }
                    else {
                        $skipped.Add($result) | Out-Null
                    }
                }
            }
            continue
        }

        if ((Test-Path -LiteralPath $targetPath) -and (-not $Force)) {
            $skipped.Add([pscustomobject]@{
                    action = "state_preserved"
                    source = $sourcePath
                    target = $targetPath
                }) | Out-Null
            continue
        }

        $result = Copy-ManagedFile -SourcePath $sourcePath -TargetPath $targetPath -AllowOverwrite:$true
        switch ([string]$result.action) {
            "copied" { $copied.Add($result) | Out-Null }
            "unchanged" { $unchanged.Add($result) | Out-Null }
            default {
                if ($required) {
                    $errors.Add($result) | Out-Null
                }
                else {
                    $skipped.Add($result) | Out-Null
                }
            }
        }
        continue
    }

    if ($kind -eq "directory") {
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            $missingResult = [pscustomobject]@{
                action = "missing_source_directory"
                source = $sourcePath
                target = $targetPath
            }

            if ($required) {
                $errors.Add($missingResult) | Out-Null
            }
            else {
                $skipped.Add($missingResult) | Out-Null
            }

            continue
        }

        $sourceFiles = Get-ChildItem -LiteralPath $sourcePath -File -Recurse
        foreach ($sourceFile in @($sourceFiles)) {
            $relativeChild = $sourceFile.FullName.Substring($sourcePath.Length).TrimStart('\', '/')
            $targetChild = Join-Path $targetPath $relativeChild
            $result = Copy-ManagedFile -SourcePath $sourceFile.FullName -TargetPath $targetChild -AllowOverwrite:$Force

            switch ([string]$result.action) {
                "copied" { $copied.Add($result) | Out-Null }
                "unchanged" { $unchanged.Add($result) | Out-Null }
                "conflict" { $conflicts.Add($result) | Out-Null }
                default {
                    if ($required) {
                        $errors.Add($result) | Out-Null
                    }
                    else {
                        $skipped.Add($result) | Out-Null
                    }
                }
            }
        }

        continue
    }

    $errors.Add([pscustomobject]@{
            action = "unknown_entry_kind"
            source = $sourcePath
            target = $targetPath
            kind = $kind
        }) | Out-Null
}

$statePath = ""
if ($null -ne $manifest.PSObject.Properties["state_seed"] -and $null -ne $manifest.state_seed.PSObject.Properties["path"]) {
    $statePath = [string]$manifest.state_seed.path
}

if (-not [string]::IsNullOrWhiteSpace($statePath)) {
    $stateSeedMode = ""
    if ($null -ne $manifest.state_seed.PSObject.Properties["mode"]) {
        $stateSeedMode = [string]$manifest.state_seed.mode
    }
    if (-not [string]::IsNullOrWhiteSpace($Mode) -and -not [string]::IsNullOrWhiteSpace($stateSeedMode) -and $Mode -ne $stateSeedMode) {
        $skipped.Add([pscustomobject]@{
                path = $statePath
                reason = "mode_filter_state_seed"
                mode = $Mode
                seed_mode = $stateSeedMode
            }) | Out-Null
        $statePath = ""
    }
}

if (-not [string]::IsNullOrWhiteSpace($statePath)) {
    $targetStatePath = Join-Path $targetRoot $statePath
    $stateAction = $null

    if ($CopyStateFromSource) {
        $sourceStatePath = Join-Path $sourceRoot $statePath
        $stateAction = Copy-ManagedFile -SourcePath $sourceStatePath -TargetPath $targetStatePath -AllowOverwrite:$Force
    }
    else {
        if ((Test-Path -LiteralPath $targetStatePath) -and (-not $Force)) {
            $stateAction = [pscustomobject]@{
                action = "state_preserved"
                source = $null
                target = $targetStatePath
            }
        }
        else {
            Ensure-ParentDirectory -Path $targetStatePath
            $projectValue = if ([string]::IsNullOrWhiteSpace($ProjectName)) { Split-Path -Leaf $targetRoot } else { $ProjectName }
            $modeValue = if (-not [string]::IsNullOrWhiteSpace($Mode)) { $Mode } elseif ($null -ne $manifest.state_seed.PSObject.Properties["mode"]) { [string]$manifest.state_seed.mode } else { "autonomous-refactor-loop" }

            $seedState = [ordered]@{
                project = $projectValue
                mode = $modeValue
                updated_at = [DateTime]::UtcNow.ToString("o")
                current_task = $null
                tasks = [ordered]@{}
                blocked = @()
                history = @()
                last_summary = ""
            }

            $seedState | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $targetStatePath -Encoding UTF8
            $stateAction = [pscustomobject]@{
                action = "state_seeded"
                source = $null
                target = $targetStatePath
            }
        }
    }

    switch ([string]$stateAction.action) {
        "copied" { $copied.Add($stateAction) | Out-Null }
        "unchanged" { $unchanged.Add($stateAction) | Out-Null }
        "state_seeded" { $copied.Add($stateAction) | Out-Null }
        "state_preserved" { $skipped.Add($stateAction) | Out-Null }
        "conflict" { $conflicts.Add($stateAction) | Out-Null }
        default { $errors.Add($stateAction) | Out-Null }
    }
}

$status = if ($errors.Count -gt 0) {
    "failed"
}
elseif ($conflicts.Count -gt 0) {
    "needs_force_or_manual"
}
else {
    "ok"
}

$result = [ordered]@{
    status = $status
    source_repo_root = $sourceRoot
    target_repo_root = $targetRoot
    copied_count = $copied.Count
    unchanged_count = $unchanged.Count
    skipped_count = $skipped.Count
    conflicts_count = $conflicts.Count
    errors_count = $errors.Count
    copied = @($copied)
    unchanged = @($unchanged)
    skipped = @($skipped)
    conflicts = @($conflicts)
    errors = @($errors)
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 100
}
else {
    $result
}

if ($status -eq "ok") {
    exit 0
}

if ($status -eq "needs_force_or_manual") {
    exit 2
}

exit 1
