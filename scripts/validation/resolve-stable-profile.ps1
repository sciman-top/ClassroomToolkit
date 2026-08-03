param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("locked-restore")]
    [string]$Pipeline,
    [Parameter(Mandatory = $true)]
    [string]$EventName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$normalizedEvent = if ([string]::IsNullOrWhiteSpace($EventName)) { "" } else { $EventName.Trim().ToLowerInvariant() }
$isPush = $normalizedEvent -eq "push"

switch ($Pipeline)
{
    "locked-restore" {
        if ($isPush) { "standard" } else { "quick" }
        break
    }
    default {
        throw "Unsupported pipeline: $Pipeline"
    }
}
