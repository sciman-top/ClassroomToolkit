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
if ($normalizedEvent -ne "push" -and $normalizedEvent -ne "pull_request" -and $normalizedEvent -ne "") {
    Write-Warning "[stable-profile] Unrecognized event '$EventName'; defaulting to standard profile."
}

switch ($Pipeline)
{
    # PR 与 push 同档：stable 套件约 10 秒即可跑完，PR 档位弱于 push 会把回归
    # 暴露推迟到合并之后，返工成本远高于 PR 阶段的增量门禁时间。
    "locked-restore" {
        "standard"
        break
    }
    default {
        throw "Unsupported pipeline: $Pipeline"
    }
}
