[CmdletBinding()]
param(
    [switch]$PassThru
)

$initializeWindowsProcessEnvironment = {
    param([bool]$EmitApplied)

    function Get-ProcessEnvironmentValue {
        param([Parameter(Mandatory = $true)][string]$Name)
        return [Environment]::GetEnvironmentVariable($Name, "Process")
    }

    function Resolve-UserProfilePath {
        $userProfile = Get-ProcessEnvironmentValue -Name "USERPROFILE"
        if (-not [string]::IsNullOrWhiteSpace($userProfile)) {
            return $userProfile
        }

        try {
            $specialFolder = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
            if (-not [string]::IsNullOrWhiteSpace($specialFolder)) {
                return $specialFolder
            }
        }
        catch {
            # Use deterministic Windows fallbacks below.
        }

        $homeDrive = Get-ProcessEnvironmentValue -Name "HOMEDRIVE"
        $homePath = Get-ProcessEnvironmentValue -Name "HOMEPATH"
        if (-not [string]::IsNullOrWhiteSpace($homeDrive) -and -not [string]::IsNullOrWhiteSpace($homePath)) {
            return Join-Path $homeDrive $homePath
        }

        $username = Get-ProcessEnvironmentValue -Name "USERNAME"
        if (-not [string]::IsNullOrWhiteSpace($username)) {
            return Join-Path "C:\Users" $username
        }

        return $null
    }

    function Set-ProcessEnvironmentDefault {
        param(
            [Parameter(Mandatory = $true)][string]$Name,
            [Parameter()][AllowNull()][string]$Value
        )

        if ([string]::IsNullOrWhiteSpace($Value)) {
            return $null
        }

        $current = Get-ProcessEnvironmentValue -Name $Name
        if (-not [string]::IsNullOrWhiteSpace($current)) {
            return $null
        }

        [Environment]::SetEnvironmentVariable($Name, $Value, "Process")
        return [pscustomobject]@{
            name = $Name
            value = $Value
        }
    }

    function Add-ProcessPathPrefix {
        param([Parameter()][AllowNull()][string]$PathEntry)

        if ([string]::IsNullOrWhiteSpace($PathEntry)) {
            return $null
        }
        if (-not (Test-Path -LiteralPath $PathEntry)) {
            return $null
        }

        $currentPath = [Environment]::GetEnvironmentVariable("PATH", "Process")
        $parts = @($currentPath -split [IO.Path]::PathSeparator | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $exists = @($parts | Where-Object { $_.TrimEnd("\") -ieq $PathEntry.TrimEnd("\") }).Count -gt 0
        if ($exists) {
            return $null
        }

        [Environment]::SetEnvironmentVariable("PATH", ($PathEntry + [IO.Path]::PathSeparator + $currentPath), "Process")
        return [pscustomobject]@{
            name = "PATH"
            value = $PathEntry
        }
    }

    $userProfile = Resolve-UserProfilePath
    $windowsRoot = Get-ProcessEnvironmentValue -Name "SystemRoot"
    if ([string]::IsNullOrWhiteSpace($windowsRoot)) {
        $windowsRoot = Get-ProcessEnvironmentValue -Name "windir"
    }
    if ([string]::IsNullOrWhiteSpace($windowsRoot)) {
        $windowsRoot = "C:\Windows"
    }

    $programFiles = Get-ProcessEnvironmentValue -Name "ProgramFiles"
    if ([string]::IsNullOrWhiteSpace($programFiles)) {
        $programFiles = "C:\Program Files"
    }

    $programFilesX86 = Get-ProcessEnvironmentValue -Name "ProgramFiles(x86)"
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        $programFilesX86 = "C:\Program Files (x86)"
    }

    $defaults = [ordered]@{
        "USERPROFILE" = $userProfile
        "HOME" = $userProfile
        "SystemRoot" = $windowsRoot
        "windir" = $windowsRoot
        "ComSpec" = (Join-Path $windowsRoot "System32\cmd.exe")
        "APPDATA" = $(if (-not [string]::IsNullOrWhiteSpace($userProfile)) { Join-Path $userProfile "AppData\Roaming" } else { $null })
        "LOCALAPPDATA" = $(if (-not [string]::IsNullOrWhiteSpace($userProfile)) { Join-Path $userProfile "AppData\Local" } else { $null })
        "ProgramData" = "C:\ProgramData"
        "ProgramFiles" = $programFiles
        "ProgramFiles(x86)" = $programFilesX86
        "CommonProgramFiles" = (Join-Path $programFiles "Common Files")
        "CommonProgramFiles(x86)" = (Join-Path $programFilesX86 "Common Files")
        "NUGET_PACKAGES" = $(if (-not [string]::IsNullOrWhiteSpace($userProfile)) { Join-Path $userProfile ".nuget\packages" } else { $null })
        "NUGET_HTTP_CACHE_PATH" = $(if (-not [string]::IsNullOrWhiteSpace($userProfile)) { Join-Path $userProfile "AppData\Local\NuGet\v3-cache" } else { $null })
        "NUGET_PLUGINS_CACHE_PATH" = $(if (-not [string]::IsNullOrWhiteSpace($userProfile)) { Join-Path $userProfile "AppData\Local\NuGet\plugins-cache" } else { $null })
        "NUGET_SCRATCH" = $(if (-not [string]::IsNullOrWhiteSpace($userProfile)) { Join-Path $userProfile "AppData\Local\Temp\NuGetScratch" } else { $null })
    }

    $applied = New-Object System.Collections.Generic.List[object]
    foreach ($entry in $defaults.GetEnumerator()) {
        $result = Set-ProcessEnvironmentDefault -Name $entry.Key -Value $entry.Value
        if ($null -ne $result) {
            [void]$applied.Add($result)
        }
    }
    foreach ($pathEntry in @(
        (Join-Path $programFiles "PowerShell\7"),
        (Join-Path $windowsRoot "System32"),
        $windowsRoot,
        (Join-Path $windowsRoot "System32\WindowsPowerShell\v1.0")
    )) {
        $result = Add-ProcessPathPrefix -PathEntry $pathEntry
        if ($null -ne $result) {
            [void]$applied.Add($result)
        }
    }

    if ($EmitApplied) {
        return $applied
    }
}

& $initializeWindowsProcessEnvironment $PassThru.IsPresent
