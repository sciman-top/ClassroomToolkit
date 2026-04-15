param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$templatePath = Join-Path $repoPath "docs\change-evidence\template.md"
if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
  throw "evidence template not found: $templatePath"
}

$requiredFields = @(
  "friction_cost_signal=",
  "rollout_decision=",
  "downgrade_reason=",
  "retirement_reason=",
  "replay_ready_evidence_links="
)

$raw = Get-Content -LiteralPath $templatePath -Raw
$missing = [System.Collections.Generic.List[string]]::new()
foreach ($field in $requiredFields) {
  if ($raw.IndexOf($field, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
    [void]$missing.Add($field.TrimEnd('='))
  }
}

$result = [pscustomobject]@{
  schema_version = "1.0"
  repo_root = ($repoPath -replace '\\', '/')
  template_path = ($templatePath -replace '\\', '/')
  required_field_count = [int]$requiredFields.Count
  missing_field_count = [int]$missing.Count
  missing_fields = @($missing)
}

if ($AsJson.IsPresent) {
  $result | ConvertTo-Json -Depth 6 | Write-Output
  if ($result.missing_field_count -gt 0) { exit 1 }
  exit 0
}

Write-Host ("evidence_template.required_field_count={0}" -f $result.required_field_count)
Write-Host ("evidence_template.missing_field_count={0}" -f $result.missing_field_count)
if ($result.missing_field_count -gt 0) {
  Write-Host ("evidence_template.missing_fields={0}" -f ([string]::Join(";", @($result.missing_fields))))
  exit 1
}
Write-Host "[PASS] evidence template fields check passed"
exit 0
