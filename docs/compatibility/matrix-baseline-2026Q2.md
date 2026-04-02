# Compatibility Matrix Baseline 2026Q2

Date: 2026-04-02
Owner: Engineering
Scope: baseline known-good combinations for classroom release acceptance.

| ID | OS | App Arch | Runtime | Presentation | Edition | Presentation Arch | Privilege Match | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| BL-01 | Win10 22H2 (19045) | x64 | .NET 10 | PowerPoint | Microsoft 365 Current | x64 | Yes | Verified | report:20260402-205900-SCIMAN-HOME-BL-01.md |
| BL-02 | Win11 23H2/24H2 | x64 | .NET 10 | PowerPoint | LTSC / volume channel | x64 | Yes | Pending Verify | Enterprise channel |
| BL-03 | Win10 22H2 (19045) | x64 | .NET 10 | WPS | Standard | x64 | Yes | Pending Verify | WPS hook path |
| BL-04 | Win11 23H2/24H2 | x64 | .NET 10 | WPS | Education/Gov | x64 | Yes | Pending Verify | May require override package |
| BL-05 | Win10/11 | x64 | .NET 10 | PowerPoint/WPS | Mixed | x86 | Yes | Pending Verify | Should warn for bitness mismatch |
| BL-06 | Win10/11 | x64 | .NET 10 | PowerPoint/WPS | Any | x64 | No | Pending Verify | Must block on privilege mismatch |

## Execution Notes
- Fill each row using `docs/compatibility/matrix-template.md`.
- Any `Pending Verify` row cannot be promoted to production SLA without evidence.
