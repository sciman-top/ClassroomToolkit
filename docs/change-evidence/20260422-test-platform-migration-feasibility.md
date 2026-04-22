# 2026-04-22 Test Platform Migration Feasibility

## Summary

- scope: `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- target: migrate remaining `Microsoft.Testing.Platform*` and `Microsoft.ApplicationInsights` residuals
- result: blocked on upstream stable-package constraints from `xunit.v3` line
- risk level: high if forced (requires pre-release test stack)

## Basis

1. Current outdated residuals are only:
   - `Microsoft.Testing.Platform* 1.9.1 -> 2.2.1`
   - `Microsoft.ApplicationInsights 2.23.0 -> 3.1.0`
2. Installed stable `xunit.v3` line is `3.2.2` and package metadata indicates `Microsoft.Testing.Platform 1.9.1` and `Microsoft.Testing.Extensions.Telemetry 1.9.1` as fixed dependencies for `xunit.v3.core.mtp-v1`.
3. `Microsoft.Testing.Extensions.Telemetry 1.9.1` package metadata in turn pins `Microsoft.ApplicationInsights 2.23.0`.

## Evidence

- cmd: `dotnet list tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj package --outdated --include-transitive`
  - key_output: only `Microsoft.Testing.Platform*` and `Microsoft.ApplicationInsights` remain.
- cmd: `Get-Content C:\Users\sciman\.nuget\packages\xunit.v3.core.mtp-v1\3.2.2\xunit.v3.core.mtp-v1.nuspec`
  - key_output: dependencies include `Microsoft.Testing.Platform` `1.9.1`, `Microsoft.Testing.Extensions.Telemetry` `1.9.1`, `Microsoft.Testing.Extensions.TrxReport.Abstractions` `1.9.1`, `Microsoft.Testing.Platform.MSBuild` `1.9.1`.
- cmd: `Get-Content C:\Users\sciman\.nuget\packages\microsoft.testing.extensions.telemetry\1.9.1\microsoft.testing.extensions.telemetry.nuspec`
  - key_output: dependency includes `Microsoft.ApplicationInsights` `2.23.0`.
- cmd: `dotnet list tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj package --outdated --include-transitive --include-prerelease`
  - key_output: available migration path for xUnit itself is currently pre-release (`xunit.v3 4.0.0-pre.*`, `xunit.runner.visualstudio 4.0.0-pre.*`), not stable.

## Gate Validation

- cmd: `dotnet build ClassroomToolkit.sln -c Debug -m:1`
  - exit_code: `0`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
  - exit_code: `0`
  - key_output: `3406` passed
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `28` passed
- cmd: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: hotspot pass

## Conclusion

- In the current stable dependency set, this residual cannot be safely auto-fixed.
- Closing it requires one of:
  - wait for a stable `xunit.v3` line that lifts `MTP v1` pins; or
  - intentionally adopt pre-release xUnit/test-platform packages in a dedicated migration branch with rollback rehearsal.

## Rollback

- no code/path behavior change introduced in this feasibility slice.
- rollback action: remove this evidence file if not needed.
