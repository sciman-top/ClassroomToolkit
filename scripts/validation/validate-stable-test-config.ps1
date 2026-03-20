param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot "stable-test-filters.json")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ConfigPath))
{
    throw "[stable-tests-config] Config file not found: $ConfigPath"
}

$raw = Get-Content -LiteralPath $ConfigPath -Raw
$config = $raw | ConvertFrom-Json
if ($null -eq $config)
{
    throw "[stable-tests-config] Config JSON is empty: $ConfigPath"
}

if ($null -eq $config.profiles)
{
    throw "[stable-tests-config] Missing 'profiles' object: $ConfigPath"
}

$requiredProfiles = @("quick", "standard", "full")
foreach ($name in $requiredProfiles)
{
    $profileFilters = $config.profiles.$name
    if ($null -eq $profileFilters)
    {
        throw "[stable-tests-config] Missing required profile '$name'."
    }

    $filters = @($profileFilters)
    if ($filters.Count -eq 0)
    {
        throw "[stable-tests-config] Profile '$name' must not be empty."
    }

    for ($i = 0; $i -lt $filters.Count; $i++)
    {
        $value = $filters[$i]
        if ($null -eq $value)
        {
            throw "[stable-tests-config] Profile '$name' contains null at index $i."
        }

        if ($value -isnot [string])
        {
            throw "[stable-tests-config] Profile '$name' index $i must be string."
        }

        $trimmed = $value.Trim()
        if (($name -ne "full") -and [string]::IsNullOrWhiteSpace($trimmed))
        {
            throw "[stable-tests-config] Profile '$name' index $i must not be empty."
        }
    }
}

Write-Host "[stable-tests-config] Validated successfully: $ConfigPath"
