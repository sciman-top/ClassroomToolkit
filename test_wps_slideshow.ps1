# Test WPS slideshow start via COM and page tracking
try {
    Write-Host "Connecting to WPS..."
    $wps = [Runtime.InteropServices.Marshal]::GetActiveObject('KWPP.Application')
    
    if ($wps.Presentations.Count -eq 0) {
        Write-Host "Please open a PPT file in WPS first!"
        exit
    }
    
    Write-Host ("Starting slideshow for: " + $wps.ActivePresentation.Name)
    $null = $wps.ActivePresentation.SlideShowSettings.Run()
    
    Start-Sleep -Seconds 1
    Write-Host "Slideshow started. Now flip some pages and run this again to check current page."
    Write-Host ""
    
    if ($wps.SlideShowWindows.Count -gt 0) {
        $view = $wps.SlideShowWindows.Item(1).View
        Write-Host ("Current Page: " + $view.CurrentShowPosition)
        Write-Host ("SlideIndex: " + $view.Slide.SlideIndex)
        Write-Host ("SlideID: " + $view.Slide.SlideID)
    }
} catch {
    Write-Host ("Error: " + $_.Exception.Message)
}
