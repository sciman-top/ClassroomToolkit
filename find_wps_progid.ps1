$ErrorActionPreference = "Continue"

function Test-ProgID {
    param($name)
    Write-Host -NoNewline "Testing ProgID '$name'... "
    try {
        $type = [Type]::GetTypeFromProgID($name)
        if ($type) {
            Write-Host "FOUND!" -ForegroundColor Green
            return $true
        } else {
            Write-Host "Not found." -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host "Error: $($_.Exception.Message)"
        return $false
    }
}

Write-Host "=== Validating WPS ProgIDs ==="
$candidates = @("KWPP.Application", "WPP.Application", "WPS.Application", "Kingsoft.Application")

foreach ($id in $candidates) {
    Test-ProgID $id
}

Write-Host "`n=== Registry Search ==="
# Try to find mostly likely ProgIDs associated with .dps or .pptx if possible, or just scan HKCR
# Provide a simplified scan for Kingsoft
$regPaths = @("HKLM:\SOFTWARE\Classes", "HKCU:\Software\Classes")
foreach ($path in $regPaths) {
    if (Test-Path $path) {
        Get-ChildItem $path -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*WPP.Application*" -or $_.PSChildName -like "*WPS.Application*" } | Select-Object PSChildName
    }
}
