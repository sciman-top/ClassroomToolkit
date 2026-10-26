# 共享脚本 helper：release/quality/validation 各入口统一导入。
# 行为契约由 ReleaseArtifactContractTests / ReleasePrepareDistributionContractTests 及各入口实际执行钉定。
function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path (Get-Location) $Path
}

function Resolve-ReleaseSourceCommit {
    param(
        [Parameter(Mandatory = $true)][string]$SourceRef,
        [Parameter()][string]$ResolvedSourceCommit = "",
        [Parameter(Mandatory = $true)][string]$ScenarioLabel
    )

    $commitRef = if ([string]::IsNullOrWhiteSpace($ResolvedSourceCommit)) {
        "$SourceRef^{commit}"
    }
    else {
        "$ResolvedSourceCommit^{commit}"
    }

    $commit = (& git rev-parse --verify --end-of-options $commitRef).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
        throw "Source reference does not resolve to a commit: $commitRef"
    }

    $statusLines = @(& git status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect the Git worktree before $ScenarioLabel packaging."
    }
    if ($statusLines.Count -gt 0) {
        $scenarioTitle = "$([char]::ToUpperInvariant($ScenarioLabel[0]))$($ScenarioLabel.Substring(1)) packaging"
        throw "$scenarioTitle requires a clean checkout/worktree; refusing to mix working-tree files with committed source."
    }

    $currentCommit = (& git rev-parse --verify --end-of-options "HEAD^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or $currentCommit -ne $commit) {
        throw "Current checkout HEAD '$currentCommit' does not match resolved source commit '$commit'."
    }

    return $commit
}

Export-ModuleMember -Function @('Resolve-AbsolutePath', 'Resolve-ReleaseSourceCommit')
