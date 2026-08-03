# sciman Classroom Toolkit

[中文](./README.md) | English

> A Windows-first classroom toolkit for roll call, timers, annotation, image/PDF presentation, and PowerPoint/WPS slideshow control.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

## Project Focus

This repository is designed for common classroom actions on a single Windows teaching PC:

- Random roll call and in-class interaction
- Countdown timers, stopwatches, and activity timing
- Screen annotation with touch, pen displays, stylus tablets, and mouse
- Full-screen image and PDF presentation with navigation, zoom, and pan
- PowerPoint / WPS slideshow navigation with overlay annotation
- A floating launcher for quick tool switching during lessons

Out of scope:

- School administration, grading, assignments, or SIS workflows
- Mandatory cloud accounts, server-side sync, or online collaboration
- Breaking the compatibility of `students.xlsx`, `student_photos/`, or `settings.ini`
- Cross-platform support beyond Windows desktop environments

## Requirements

- Windows 10 or Windows 11
- Packaged releases are recommended for normal classroom use
- `.NET 10 SDK` for development
- Optional hardware: touch display, pen tablet, presentation remote, projector, or external monitor

## Current Status (2026-06-13)

Recent work has focused on high-frequency classroom flows and touch-first stability:

- In full-screen image / PDF mode, the board button now offers `capture to board / plain board / colored board` instead of jumping straight into board mode.
- The 3 quick-brush slots now keep independent sizes. Tapping the same quick brush again opens color and 3 size choices.
- Undo for image / PDF annotation now restores runtime history, cache, and persistence state, so ink does not disappear again after pan / zoom.
- Student photo overlay, toolbar, roll-call window, and launcher z-order behavior has been hardened, especially on first show and re-show.
- Paint settings dialog construction is now guarded against initialization-time null-reference crashes.

Latest local verification snapshot:

- `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings / 0 errors
- contract / invariant subset: passed, 29/29
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: currently `3533` passed, `0` failed
- Current code blocker: none
  - The photo-overlay null-bitmap failure branch contract now matches the `EnterInactivePassthroughState()` transparent passthrough behavior and no longer expects `Hide()`

For more context:

- [Documentation index](./docs/README.md)
- [Current handover](./docs/handover.md)
- [Recent change evidence](./docs/change-evidence/)

## Quick Start

### For Teachers

1. Download a packaged release from GitHub Releases.
2. Extract it to a stable folder and run `sciman Classroom Toolkit.exe`.
3. Confirm that the floating launcher appears, then verify roll call, image / PDF viewing, board entry, and PPT / WPS annotation.

Daily classroom usage is documented in the [Teacher Guide](./使用指南.md).

### For Developers

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

To prepare release packages, prefer the built-in scripts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Release -Profile full
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/prepare-distribution.ps1 -Version <version> -PackageMode all -Configuration Release -EnsureLatestRuntime
```

## Local Data

The app primarily reads three local resources:

- `students.xlsx`: student roster workbook
- `student_photos/`: student photo directory
- `settings.ini`: local compatibility settings file

Suggested photo structure:

```text
student_photos/
├── Class 1/
│   ├── 001.jpg
│   └── 002.png
└── Class 2/
    └── 101.jpg
```

Data conventions:

- Each worksheet in `students.xlsx` represents one class
- Photo folders are grouped by class
- File names should preferably use student IDs
- Supported formats: `.jpg`, `.jpeg`, `.png`, `.bmp`
- The app can generate a template when no roster is found
- Any format change must preserve compatibility with existing classroom machines and files

## Repository Layout

```text
src/ClassroomToolkit.App          WPF UI, startup flow, windows, and classroom session orchestration
src/ClassroomToolkit.Application  Application use cases and cross-module coordination
src/ClassroomToolkit.Domain       Core rules and business models
src/ClassroomToolkit.Services     Runtime bridges and application services
src/ClassroomToolkit.Infra        Configuration, persistence, and filesystem details
src/ClassroomToolkit.Interop      Win32 / COM / WPS integration boundaries
tests/ClassroomToolkit.Tests      Automated tests
scripts/                         Quality gates, validation, release, and environment scripts
docs/                            Architecture, plans, validation, evidence, and runbooks
```

## Build and Verification

The fixed delivery gate order is `build -> test -> contract/invariant -> hotspot`:

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate!=CoreContract"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate=CoreContract"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

The repository also provides an aggregate quality gate:

```powershell
powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

Use `quick` for local feedback, `standard` for the normal delivery gate plus vulnerability scanning, and `full` before a release for dependency-update and `latest-all` analyzer audits. Available dependency updates no longer block routine code delivery, but remain explicit release-audit failures until upgraded or waived.

For documentation-only changes, run at least:

```powershell
git diff --check
```

If you are continuing from the current main branch, read [docs/handover.md](./docs/handover.md) first for the current verification snapshot; code gates are green, while release still needs classroom-site validation.

## Documentation

- [Chinese README](./README.md)
- [Teacher Guide](./使用指南.md)
- [Documentation index](./docs/README.md)
- [Current handover](./docs/handover.md)
- [Tech debt and stability backlog](./docs/tech-debt-backlog.md)
- [Change evidence directory](./docs/change-evidence/)
- [Release checklist](./docs/runbooks/release-checklist.md)
- [Classroom pilot validation runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## Known Limitations and Release Boundary

- Windows classroom PCs remain the primary target
- Multi-monitor, DPI scaling, projector, and PPT / WPS slideshow integration still require on-site validation
- Missing runtimes, permissions, or device drivers may require school IT support
- Student rosters, photos, and settings are local files and should be backed up appropriately
- Code gates are currently green; a release baseline should still include on-site checks for multi-monitor, DPI, projector, PPT / WPS, and student-photo overlay behavior

## License

MIT
