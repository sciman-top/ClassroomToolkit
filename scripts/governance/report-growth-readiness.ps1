param(
  [string]$OutputPath = "",
  [switch]$EmitJson
)

$ErrorActionPreference = "Stop"
$kitRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commonPath = Join-Path $kitRoot "scripts\lib\common.ps1"
if (Test-Path -LiteralPath $commonPath -PathType Leaf) {
  . $commonPath
}

if (-not (Get-Command -Name Write-Utf8NoBom -ErrorAction SilentlyContinue)) {
  function Write-Utf8NoBom([string]$Path, [string]$Content) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $enc)
  }
}

if (-not (Get-Command -Name Get-CurrentPowerShellPath -ErrorAction SilentlyContinue)) {
  function Get-CurrentPowerShellPath() {
    $exe = (Get-Process -Id $PID -ErrorAction SilentlyContinue).Path
    if ([string]::IsNullOrWhiteSpace($exe)) { return "powershell" }
    return $exe
  }
}

$verifyScript = Join-Path $PSScriptRoot "verify-growth-pack.ps1"
if (-not (Test-Path -LiteralPath $verifyScript -PathType Leaf)) {
  throw "Missing script: $verifyScript"
}

$psExe = Get-CurrentPowerShellPath
$raw = & $psExe -NoProfile -ExecutionPolicy Bypass -File $verifyScript -AsJson
$verifyExit = $LASTEXITCODE
$jsonText = [string]::Join([Environment]::NewLine, @($raw))
if ([string]::IsNullOrWhiteSpace($jsonText)) {
  throw "verify-growth-pack returned empty output"
}
$result = $jsonText | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
  $reportName = "growth-readiness-{0}.md" -f (Get-Date -Format "yyyyMMdd-HHmmss")
  $OutputPath = Join-Path $kitRoot ("docs\change-evidence\" + $reportName)
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# Growth Readiness Report") | Out-Null
$lines.Add("") | Out-Null
$lines.Add(("generated_at={0}" -f $result.generated_at)) | Out-Null
$lines.Add(("repo_count={0}" -f $result.repo_count)) | Out-Null
$lines.Add(("failed_count={0}" -f $result.failed_count)) | Out-Null
$lines.Add(("status={0}" -f $result.status)) | Out-Null
$lines.Add("") | Out-Null

foreach ($item in @($result.items)) {
  $lines.Add(("## {0}" -f $item.repo_name)) | Out-Null
  $lines.Add(("status={0}" -f $item.status)) | Out-Null
  $lines.Add(("readiness_score={0}" -f $item.readiness_score)) | Out-Null
  $lines.Add(("coverage={0}/{1}" -f $item.present_count, $item.expected_count)) | Out-Null
  if (@($item.missing_files).Count -gt 0) {
    $lines.Add(("missing_files={0}" -f ((@($item.missing_files)) -join ", "))) | Out-Null
  }
  if (@($item.advisory).Count -gt 0) {
    $lines.Add(("advisory={0}" -f ((@($item.advisory)) -join " | "))) | Out-Null
  }
  $lines.Add("") | Out-Null
}

$outDir = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outDir -PathType Container)) {
  New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
Write-Utf8NoBom -Path $OutputPath -Content ([string]::Join([Environment]::NewLine, $lines))

Write-Host ("growth_readiness_report=" + ($OutputPath -replace '\\', '/'))
if ($EmitJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
}

if ($verifyExit -eq 0 -and [string]$result.status -eq "PASS") { exit 0 }
exit 1
