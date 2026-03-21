param(
    [string]$SuggestionsPath = "",
    [string]$OutputPath = "",
    [string]$SettingsPath = "",
    [switch]$ApplyToSettings,
    [string]$BackupSuffix = ".bak"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

function Normalize-TokenArray {
    param([object]$Values)

    if ($null -eq $Values) {
        return @()
    }

    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($value in @($Values)) {
        $token = [string]$value
        if ([string]::IsNullOrWhiteSpace($token)) {
            continue
        }
        $normalized = $token.Trim()
        if ($normalized.Length -lt 3) {
            continue
        }
        [void]$set.Add($normalized)
    }

    return @($set | Sort-Object)
}

$repoRoot = (Resolve-Path -LiteralPath ".").Path
$effectiveSuggestionsPath = Resolve-AbsolutePath -Path $SuggestionsPath -BasePath $repoRoot
if ([string]::IsNullOrWhiteSpace($effectiveSuggestionsPath) -or -not (Test-Path -LiteralPath $effectiveSuggestionsPath)) {
    throw "Suggestions file not found: $effectiveSuggestionsPath"
}

$suggestions = Get-Content -LiteralPath $effectiveSuggestionsPath -Raw | ConvertFrom-Json
$tokenSuggestions = $suggestions.token_suggestions
if ($null -eq $tokenSuggestions) {
    throw "Invalid suggestions file: token_suggestions is missing."
}

$overridesObject = [ordered]@{
    additionalWpsClassTokens = Normalize-TokenArray -Values $tokenSuggestions.additionalWpsClassTokens
    additionalOfficeClassTokens = Normalize-TokenArray -Values $tokenSuggestions.additionalOfficeClassTokens
    additionalSlideshowClassTokens = Normalize-TokenArray -Values $tokenSuggestions.additionalSlideshowClassTokens
    additionalWpsProcessTokens = Normalize-TokenArray -Values $tokenSuggestions.additionalWpsProcessTokens
    additionalOfficeProcessTokens = Normalize-TokenArray -Values $tokenSuggestions.additionalOfficeProcessTokens
}

$compactOverridesJson = $overridesObject | ConvertTo-Json -Depth 10 -Compress
$prettyOverridesJson = $overridesObject | ConvertTo-Json -Depth 10

$defaultOutputPath = Join-Path (Split-Path -Parent $effectiveSuggestionsPath) "classifier-overrides.fragment.json"
$effectiveOutputPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $defaultOutputPath
}
else {
    Resolve-AbsolutePath -Path $OutputPath -BasePath $repoRoot
}

$result = [ordered]@{
    generated_at_utc = [DateTime]::UtcNow.ToString("o")
    suggestions_path = $effectiveSuggestionsPath
    overrides_json_compact = $compactOverridesJson
    overrides_json_pretty = $prettyOverridesJson
    settings_patch = [ordered]@{
        section = "Paint"
        key = "presentation_classifier_overrides_json"
        value = $compactOverridesJson
    }
}

$outputDir = Split-Path -Parent $effectiveOutputPath
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $effectiveOutputPath -Encoding UTF8
Write-Host "CLASSIFIER_OVERRIDES_FRAGMENT: $effectiveOutputPath"

if ($ApplyToSettings.IsPresent) {
    $effectiveSettingsPath = Resolve-AbsolutePath -Path $SettingsPath -BasePath $repoRoot
    if ([string]::IsNullOrWhiteSpace($effectiveSettingsPath) -or -not (Test-Path -LiteralPath $effectiveSettingsPath)) {
        throw "Settings file not found: $effectiveSettingsPath"
    }

    $raw = Get-Content -LiteralPath $effectiveSettingsPath -Raw
    $settingsJson = $raw | ConvertFrom-Json
    if ($null -eq $settingsJson.PSObject.Properties["Paint"]) {
        $settingsJson | Add-Member -MemberType NoteProperty -Name "Paint" -Value ([pscustomobject]@{})
    }

    $backupPath = "$effectiveSettingsPath$BackupSuffix"
    Copy-Item -LiteralPath $effectiveSettingsPath -Destination $backupPath -Force
    if ($null -eq $settingsJson.Paint.PSObject.Properties["presentation_classifier_overrides_json"]) {
        $settingsJson.Paint | Add-Member -MemberType NoteProperty -Name "presentation_classifier_overrides_json" -Value $compactOverridesJson
    }
    else {
        $settingsJson.Paint.presentation_classifier_overrides_json = $compactOverridesJson
    }
    $settingsJson | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $effectiveSettingsPath -Encoding UTF8
    Write-Host "SETTINGS_PATCH_APPLIED: $effectiveSettingsPath"
    Write-Host "SETTINGS_BACKUP: $backupPath"
}
