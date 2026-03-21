param(
    [string]$SummaryPath = "",
    [string]$LogDirectory = "",
    [string]$OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-LatestChecklistSummaryPath {
    param([string]$RepoRoot)

    $summaryDir = Join-Path $RepoRoot ".codex/logs/checklist-loop"
    if (-not (Test-Path -LiteralPath $summaryDir)) {
        throw "Summary directory not found: $summaryDir"
    }

    $latest = Get-ChildItem -LiteralPath $summaryDir -File -Filter "run-*.summary.json" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if ([string]::IsNullOrWhiteSpace([string]$latest)) {
        throw "No checklist summary found in: $summaryDir"
    }

    return $latest
}

function Resolve-AbsolutePath {
    param(
        [string]$Path,
        [string]$BasePath = ""
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        if (Test-Path -LiteralPath $Path) {
            return (Resolve-Path -LiteralPath $Path).Path
        }

        return [System.IO.Path]::GetFullPath($Path)
    }

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Normalize-Token {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $token = $Value.Trim().Trim('"', '''', '[', ']', '(', ')')
    if ($token.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        $token = $token.Substring(0, $token.Length - 4)
    }
    return $token.ToLowerInvariant()
}

function Add-UniqueToken {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [string]$RawToken
    )

    $token = Normalize-Token -Value $RawToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        return
    }
    if ($token.Length -lt 3) {
        return
    }
    [void]$Set.Add($token)
}

function Add-UniqueSignal {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [string]$Signal
    )

    if ([string]::IsNullOrWhiteSpace($Signal)) {
        return
    }
    [void]$Set.Add($Signal.Trim())
}

function Classify-ProcessToken {
    param([string]$Token)

    if ($Token -match "powerp|ppt|office") {
        return "office"
    }

    if ($Token -match "wps|wpp|wppt|kwpp|kwps") {
        return "wps"
    }

    return "unknown"
}

function Add-ClassifiedProcessToken {
    param(
        [string]$RawToken,
        [System.Collections.Generic.HashSet[string]]$OfficeProcessTokens,
        [System.Collections.Generic.HashSet[string]]$WpsProcessTokens,
        [System.Collections.Generic.HashSet[string]]$UnknownProcessTokens,
        [switch]$IgnoreUnknown
    )

    $token = Normalize-Token -Value $RawToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        return
    }

    switch (Classify-ProcessToken -Token $token) {
        "office" { Add-UniqueToken -Set $OfficeProcessTokens -RawToken $token }
        "wps" { Add-UniqueToken -Set $WpsProcessTokens -RawToken $token }
        default {
            if (-not $IgnoreUnknown.IsPresent) {
                Add-UniqueToken -Set $UnknownProcessTokens -RawToken $token
            }
        }
    }
}

function Read-GateLogPathsFromSummary {
    param([object]$Summary)

    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($task in @($Summary.tasks)) {
        foreach ($gate in @($task.gates)) {
            if ($null -ne $gate.PSObject.Properties["stdout_path"] -and -not [string]::IsNullOrWhiteSpace([string]$gate.stdout_path)) {
                $paths.Add([string]$gate.stdout_path)
            }
            if ($null -ne $gate.PSObject.Properties["stderr_path"] -and -not [string]::IsNullOrWhiteSpace([string]$gate.stderr_path)) {
                $paths.Add([string]$gate.stderr_path)
            }
        }
    }

    return $paths
}

function Extract-TokensFromText {
    param(
        [string]$Text,
        [System.Collections.Generic.HashSet[string]]$OfficeProcessTokens,
        [System.Collections.Generic.HashSet[string]]$WpsProcessTokens,
        [System.Collections.Generic.HashSet[string]]$UnknownProcessTokens,
        [System.Collections.Generic.HashSet[string]]$OfficeClassTokens,
        [System.Collections.Generic.HashSet[string]]$WpsClassTokens,
        [System.Collections.Generic.HashSet[string]]$SlideshowClassTokens,
        [System.Collections.Generic.HashSet[string]]$Signals
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return
    }

    $lowerText = $Text.ToLowerInvariant()
    if ($lowerText -match "兼容|compat|presentation|ppt|wps|office|classifier|startup") {
        Add-UniqueSignal -Set $Signals -Signal $Text
    }

    foreach ($match in [regex]::Matches($Text, "(?i)override-token:([a-z0-9_.-]+)")) {
        Add-ClassifiedProcessToken `
            -RawToken $match.Groups[1].Value `
            -OfficeProcessTokens $OfficeProcessTokens `
            -WpsProcessTokens $WpsProcessTokens `
            -UnknownProcessTokens $UnknownProcessTokens
    }

    foreach ($match in [regex]::Matches($Text, "(?i)\bprocess(?:name)?\s*[:=]\s*([a-z0-9_.-]+)")) {
        Add-ClassifiedProcessToken `
            -RawToken $match.Groups[1].Value `
            -OfficeProcessTokens $OfficeProcessTokens `
            -WpsProcessTokens $WpsProcessTokens `
            -UnknownProcessTokens $UnknownProcessTokens
    }

    foreach ($match in [regex]::Matches($Text, "(?i)\bclass(?:name)?\s*[:=]\s*([a-z0-9_.-]+)")) {
        $classToken = Normalize-Token -Value $match.Groups[1].Value
        if ([string]::IsNullOrWhiteSpace($classToken)) {
            continue
        }

        if ($classToken -match "screenclass|ppt|power") {
            Add-UniqueToken -Set $OfficeClassTokens -RawToken $classToken
            Add-UniqueToken -Set $SlideshowClassTokens -RawToken $classToken
            continue
        }

        if ($classToken -match "kwpp|kwps|wpp|wps") {
            Add-UniqueToken -Set $WpsClassTokens -RawToken $classToken
            Add-UniqueToken -Set $SlideshowClassTokens -RawToken $classToken
            continue
        }

        Add-UniqueToken -Set $SlideshowClassTokens -RawToken $classToken
    }

    foreach ($match in [regex]::Matches($Text, "(?i)\b([a-z][a-z0-9_-]{2,})\((\d+)\)")) {
        Add-ClassifiedProcessToken `
            -RawToken $match.Groups[1].Value `
            -OfficeProcessTokens $OfficeProcessTokens `
            -WpsProcessTokens $WpsProcessTokens `
            -UnknownProcessTokens $UnknownProcessTokens `
            -IgnoreUnknown
    }
}

$repoRoot = (Resolve-Path -LiteralPath ".").Path
$effectiveSummaryPath = if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    Resolve-LatestChecklistSummaryPath -RepoRoot $repoRoot
}
else {
    Resolve-AbsolutePath -Path $SummaryPath -BasePath $repoRoot
}

if (-not (Test-Path -LiteralPath $effectiveSummaryPath)) {
    throw "Summary file not found: $effectiveSummaryPath"
}

$summary = Get-Content -LiteralPath $effectiveSummaryPath -Raw | ConvertFrom-Json
$effectiveLogDirectory = if (-not [string]::IsNullOrWhiteSpace($LogDirectory)) {
    Resolve-AbsolutePath -Path $LogDirectory -BasePath $repoRoot
}
elseif ($null -ne $summary.PSObject.Properties["log_directory"] -and -not [string]::IsNullOrWhiteSpace([string]$summary.log_directory)) {
    [string]$summary.log_directory
}
else {
    Split-Path -Parent $effectiveSummaryPath
}

$defaultOutputPath = Join-Path $effectiveLogDirectory ("run-{0}.classifier-suggestions.json" -f [string]$summary.run_id)
$effectiveOutputPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $defaultOutputPath
}
else {
    Resolve-AbsolutePath -Path $OutputPath -BasePath $repoRoot
}

$officeProcessTokens = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$wpsProcessTokens = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$unknownProcessTokens = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$officeClassTokens = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$wpsClassTokens = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$slideshowClassTokens = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$signals = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

$defaultProcessTokens = @(
    "powerpnt",
    "powerpoint",
    "pptview",
    "wpp",
    "wppt",
    "kwpp",
    "kwps",
    "wpspresentation"
)

$logPaths = Read-GateLogPathsFromSummary -Summary $summary
foreach ($path in $logPaths) {
    $resolved = Resolve-AbsolutePath -Path $path -BasePath $effectiveLogDirectory
    if (-not (Test-Path -LiteralPath $resolved)) {
        continue
    }

    foreach ($line in (Get-Content -LiteralPath $resolved)) {
        Extract-TokensFromText `
            -Text $line `
            -OfficeProcessTokens $officeProcessTokens `
            -WpsProcessTokens $wpsProcessTokens `
            -UnknownProcessTokens $unknownProcessTokens `
            -OfficeClassTokens $officeClassTokens `
            -WpsClassTokens $wpsClassTokens `
            -SlideshowClassTokens $slideshowClassTokens `
            -Signals $signals
    }
}

foreach ($token in $defaultProcessTokens) {
    [void]$officeProcessTokens.Remove($token)
    [void]$wpsProcessTokens.Remove($token)
    [void]$unknownProcessTokens.Remove($token)
}

$payload = [ordered]@{
    run_id = [string]$summary.run_id
    summary_path = $effectiveSummaryPath
    log_directory = $effectiveLogDirectory
    generated_at_utc = [DateTime]::UtcNow.ToString("o")
    token_suggestions = [ordered]@{
        additionalOfficeProcessTokens = @($officeProcessTokens | Sort-Object)
        additionalWpsProcessTokens = @($wpsProcessTokens | Sort-Object)
        additionalOfficeClassTokens = @($officeClassTokens | Sort-Object)
        additionalWpsClassTokens = @($wpsClassTokens | Sort-Object)
        additionalSlideshowClassTokens = @($slideshowClassTokens | Sort-Object)
        unknownProcessTokens = @($unknownProcessTokens | Sort-Object)
    }
    signal_samples = @($signals | Select-Object -First 20)
}

$outputDir = Split-Path -Parent $effectiveOutputPath
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$payload | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $effectiveOutputPath -Encoding UTF8
Write-Host "CLASSIFIER_SUGGESTIONS: $effectiveOutputPath"
