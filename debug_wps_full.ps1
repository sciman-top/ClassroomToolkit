$ErrorActionPreference = "Continue"

function Get-ComObject {
    param($progId)
    try {
        $type = [Type]::GetTypeFromProgID($progId)
        if ($null -eq $type) { Write-Host "[-] ProgID '$progId' not found in registry."; return $null }
        $obj = [System.Runtime.InteropServices.Marshal]::GetActiveObject($progId)
        Write-Host "[+] Successfully connected to '$progId'"
        return $obj
    } catch {
        Write-Host "[-] Failed to GetActiveObject for '$progId': $($_.Exception.Message)"
        return $null
    }
}

Write-Host "=== WPS Processes ==="
$ps = Get-Process wpp, wps -ErrorAction SilentlyContinue
if ($ps) {
    $ps | Format-Table Id, ProcessName, MainWindowTitle
} else {
    Write-Host "[-] No 'wpp' or 'wps' processes found."
}

Write-Host "`n=== COM Connection Test ==="
$wps = Get-ComObject "KWPP.Application"
if (-not $wps) {
    $wps = Get-ComObject "WPP.Application"
}

if (-not $wps) {
    Write-Host "`n[!] CRITICAL: Could not connect to any WPS instance via COM."
    Write-Host "This is why the 'Start Slideshow' button fails."
    exit
}

Write-Host "`n=== Presentations ==="
try {
    $count = $wps.Presentations.Count
    Write-Host "Presentations Count: $count"
    
    if ($count -eq 0) {
        Write-Host "[!] No presentations open. Please open a file in WPS."
        exit
    }
    
    $activePres = $wps.ActivePresentation
    Write-Host "Active Presentation: $($activePres.Name)"
    
    Write-Host "`n[?] Attempting to allow you to manually test SlideShowSettings.Run()..."
    Write-Host "Press ENTER to try creating a slideshow window via COM (Simulating the Button)..."
    Read-Host
    
    $ssWin = $activePres.SlideShowSettings.Run()
    Start-Sleep -Seconds 1
    
    Write-Host "SlideShowWindows.Count after Run(): $($wps.SlideShowWindows.Count)"
    
    if ($wps.SlideShowWindows.Count -gt 0) {
        $view = $wps.SlideShowWindows.Item(1).View
        Write-Host "Current Slide Index: $($view.Slide.SlideIndex)"
        Write-Host "Current Show Position: $($view.CurrentShowPosition)"
        Write-Host "[+] SUCCESS: COM-controlled slideshow is working!"
    } else {
        Write-Host "[-] FAILURE: SlideShowSettings.Run() was called but SlideShowWindows.Count is still 0."
    }
    
} catch {
    Write-Host "[-] Error inspecting presentations: $($_.Exception.Message)"
}
