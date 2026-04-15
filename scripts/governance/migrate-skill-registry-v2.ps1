param(
  [string]$RepoRoot = ".",
  [string]$RegistryRelativePath = ".governance/skill-candidates/promotion-registry.json",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NormalizedPath([string]$PathText) {
  $resolved = Resolve-Path -LiteralPath $PathText -ErrorAction Stop
  return ([System.IO.Path]::GetFullPath($resolved.Path) -replace '\\', '/').TrimEnd('/')
}

function Ensure-ParentDirectory([string]$PathText) {
  $parent = Split-Path -Parent $PathText
  if ([string]::IsNullOrWhiteSpace($parent)) { return }
  if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
  }
}

function ConvertTo-Slug([string]$Text, [int]$MaxLength = 48) {
  if ([string]::IsNullOrWhiteSpace($Text)) { return "candidate" }
  $slug = ($Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
  if ([string]::IsNullOrWhiteSpace($slug)) { return "candidate" }
  if ($slug.Length -gt $MaxLength) {
    $parts = @($slug -split '-' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $builder = ""
    foreach ($part in $parts) {
      $candidate = if ([string]::IsNullOrWhiteSpace($builder)) { $part } else { "$builder-$part" }
      if ($candidate.Length -le $MaxLength) {
        $builder = $candidate
      } else {
        break
      }
    }
    if (-not [string]::IsNullOrWhiteSpace($builder)) {
      $slug = $builder
    } else {
      $slug = $slug.Substring(0, $MaxLength).Trim('-')
    }
  }
  if ([string]::IsNullOrWhiteSpace($slug)) { return "candidate" }
  return $slug
}

function Get-SignatureSlugSeed([string]$Family) {
  $seed = if ($null -eq $Family) { "" } else { $Family.Trim().ToLowerInvariant() }
  if ([string]::IsNullOrWhiteSpace($seed)) { return "" }
  $withoutDate = ($seed -replace '-\d{8}$', '').Trim('-')
  if ([string]::IsNullOrWhiteSpace($withoutDate)) { return $seed }
  return $withoutDate
}

function Get-SignatureHash8([string]$Text) {
  $input = if ($null -eq $Text) { "" } else { $Text.ToLowerInvariant().Trim() }
  if ([string]::IsNullOrWhiteSpace($input)) { return "00000000" }
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($input)
    $hashBytes = $sha.ComputeHash($bytes)
    $hex = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    return $hex.Substring(0, 8)
  } finally {
    $sha.Dispose()
  }
}

function Get-SignatureFamily([string]$Signature, [string]$CollapsePattern = "^(.*-\d{8})-[a-z]$") {
  $raw = if ($null -eq $Signature) { "" } else { $Signature.Trim().ToLowerInvariant() }
  if ([string]::IsNullOrWhiteSpace($raw)) { return "" }
  if (-not [string]::IsNullOrWhiteSpace($CollapsePattern) -and $raw -match $CollapsePattern) {
    $base = [string]$Matches[1]
    if (-not [string]::IsNullOrWhiteSpace($base)) { return $base.Trim().ToLowerInvariant() }
  }
  return $raw
}

function Get-CanonicalSkillName([string]$Signature) {
  $family = Get-SignatureFamily -Signature $Signature
  $slug = ConvertTo-Slug $family
  return ("custom-auto-{0}" -f $slug)
}

function Normalize-StringSet([object[]]$Values) {
  $set = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($v in @($Values)) {
    $text = [string]$v
    if ([string]::IsNullOrWhiteSpace($text)) { continue }
    [void]$set.Add($text.Trim().ToLowerInvariant())
  }
  return @($set | Sort-Object)
}

$repoPath = Resolve-NormalizedPath $RepoRoot
$registryPath = Join-Path ($repoPath -replace '/', '\\') ($RegistryRelativePath -replace '/', '\\')

$now = (Get-Date).ToString("o")
$beforeSchema = "missing"
$beforeCount = 0
$migrated = $false

if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) {
  Ensure-ParentDirectory $registryPath
  $newRegistry = [ordered]@{
    schema_version = "2.0"
    registry_schema_version = 2
    lifecycle_version = "1.0"
    updated_at = $now
    promoted = @()
  }
  $newRegistry | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $registryPath -Encoding utf8
  $migrated = $true
} else {
  $raw = Get-Content -LiteralPath $registryPath -Raw
  $obj = $raw | ConvertFrom-Json
  if ($null -eq $obj.PSObject.Properties['promoted']) {
    $obj | Add-Member -NotePropertyName promoted -NotePropertyValue @()
  }

  $beforeSchema = [string]$obj.schema_version
  $beforeCount = @($obj.promoted).Count
  $collapsePattern = "^(.*-\d{8})-[a-z]$"

  $normalized = @()
  foreach ($entry in @($obj.promoted)) {
    if ($null -eq $entry) { continue }
    $sigRaw = [string]$entry.issue_signature
    if ([string]::IsNullOrWhiteSpace($sigRaw)) { continue }
    $family = Get-SignatureFamily -Signature $sigRaw -CollapsePattern $collapsePattern
    if ([string]::IsNullOrWhiteSpace($family)) { continue }

    $promotedAt = [string]$entry.promoted_at
    if ([string]::IsNullOrWhiteSpace($promotedAt)) { $promotedAt = $now }

    $hitCount = 0
    try { $hitCount = [int]$entry.hit_count } catch { $hitCount = 0 }

    $variants = @()
    $variants += $family
    if ($null -ne $entry.PSObject.Properties['signature_variants']) {
      $variants += @($entry.signature_variants)
    }
    $variants = Normalize-StringSet -Values $variants

    $repos = @()
    if ($null -ne $entry.PSObject.Properties['repos']) {
      $repos = @($entry.repos | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | Sort-Object -Unique)
    }

    $state = "active"
    if ($null -ne $entry.PSObject.Properties['lifecycle_state'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.lifecycle_state)) {
      $state = [string]$entry.lifecycle_state
    }

    $normalized += [pscustomobject]@{
      issue_signature = $family
      family_signature = $family
      skill_name = (Get-CanonicalSkillName $family)
      promoted_at = $promotedAt
      hit_count = $hitCount
      repos = $repos
      signature_variants = $variants
      lifecycle_state = $state
      invocation_count = $hitCount
      last_invoked_at = $promotedAt
      last_optimized_at = $promotedAt
      health_score = 1.0
      merged_from = @()
      quality_delta = $null
    }
  }

  $obj.schema_version = "2.0"
  if ($null -ne $obj.PSObject.Properties['registry_schema_version']) {
    $obj.registry_schema_version = 2
  } else {
    $obj | Add-Member -NotePropertyName registry_schema_version -NotePropertyValue 2
  }
  if ($null -ne $obj.PSObject.Properties['lifecycle_version']) {
    $obj.lifecycle_version = "1.0"
  } else {
    $obj | Add-Member -NotePropertyName lifecycle_version -NotePropertyValue "1.0"
  }
  if ($null -ne $obj.PSObject.Properties['updated_at']) {
    $obj.updated_at = $now
  } else {
    $obj | Add-Member -NotePropertyName updated_at -NotePropertyValue $now
  }
  $obj.promoted = @($normalized)

  $obj | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $registryPath -Encoding utf8
  $migrated = $true
}

$final = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
$afterCount = @($final.promoted).Count

$result = [ordered]@{
  ok = $true
  migrated = [bool]$migrated
  registry_path = ($registryPath -replace '\\', '/')
  schema_before = $beforeSchema
  schema_after = [string]$final.schema_version
  count_before = [int]$beforeCount
  count_after = [int]$afterCount
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
} else {
  Write-Host ("skill_registry_migrate.ok={0}" -f [bool]$result.ok)
  Write-Host ("skill_registry_migrate.schema_before={0}" -f $result.schema_before)
  Write-Host ("skill_registry_migrate.schema_after={0}" -f $result.schema_after)
  Write-Host ("skill_registry_migrate.count_before={0}" -f [int]$result.count_before)
  Write-Host ("skill_registry_migrate.count_after={0}" -f [int]$result.count_after)
  Write-Host ("skill_registry_migrate.registry_path={0}" -f $result.registry_path)
}
