# Compatibility SLA (ClassroomToolkit)

Last updated: 2026-04-02

## 1. Scope
This document defines compatibility support boundaries for ClassroomToolkit runtime behavior and release validation.

## 2. Support Tiers

### 2.1 Tier A (Officially Supported)
- OS: Windows 10 22H2+ (Build 19045+) / Windows 11 22H2+
- Architecture: x64 OS + x64 app process
- Runtime: .NET Desktop Runtime 10.x
- Presentation apps: mainstream Microsoft PowerPoint / WPS Presentation builds that match default classifier signatures or configured overrides

Expectation:
- Full quality gate required before release.
- Blocking compatibility issues must be fixed before release.

### 2.2 Tier B (Best-Effort Compatible)
- Office/WPS edition variants (education/government/custom channels)
- Environments requiring classifier override package import
- Environments with non-blocking startup warnings

Expectation:
- Allowed to run with warnings/degraded paths.
- Site-specific override package and matrix evidence required.

### 2.3 Tier C (Unsupported)
- Non-Windows OS
- Windows below Build 19045
- Non-x64 runtime architecture
- .NET runtime below 10 major

Expectation:
- Startup must block with actionable guidance.

## 3. Issue Severity Contract
- Blocking: startup blocked or contract/invariant broken.
- Warning: app starts, but one or more features may degrade.
- Info: diagnostics-only signal; no user-impacting degradation expected.

## 4. Release Gate Contract
Mandatory order:
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. contract/invariant filtered test command (project AGENTS.md)
4. `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 5. Compatibility Operations
- Every compatibility change must include:
  - matrix evidence
  - rollback action
  - known risks
- Edition-specific adaptation should prefer:
  1. classifier override package import
  2. auto-learn controlled by operator
  3. code change only when (1)(2) cannot solve

## 6. SLA for Response
- Tier A blocking compatibility bug: hotfix target <= 48h
- Tier A warning affecting classroom flow: fix target <= 5 business days
- Tier B compatibility request: evaluate + override package decision <= 5 business days
