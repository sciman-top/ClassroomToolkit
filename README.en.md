# sciman Classroom Toolkit

[中文](./README.md) | English

> A Windows-first classroom toolkit for roll call, timers, annotation, image/PDF presentation, and PowerPoint/WPS slideshow control.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

## What It Covers

- Random roll call and in-class interaction
- Timers, countdowns, and stopwatches
- Screen annotation with touch, pen displays, and stylus tablets
- Full-screen image and PDF presentation
- PowerPoint and WPS slideshow navigation with overlay tools
- A floating launcher for quick switching during lessons

## Scope

This repository intentionally does not cover:

- School administration, grading, or assignment management
- Mandatory cloud accounts or server-side sync
- Breaking changes to `students.xlsx`, `student_photos/`, or `settings.ini`
- Cross-platform support beyond Windows desktop environments

## Requirements

- Windows 10 or Windows 11
- `.NET 10 SDK` for development
- A packaged release for normal classroom use
- Optional hardware: touch screen, pen tablet, presentation remote, projector / external display

## Quick Start

### For Teachers

1. Download a release from GitHub Releases.
2. Extract it and run `sciman Classroom Toolkit.exe`.
3. Confirm the launcher opens, then verify roll call, image/PDF viewing, and slideshow annotation.

### For Developers

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

## Local Data

The app reads two local resources:

- `students.xlsx`: student roster workbook
- `student_photos/`: photo directory

Suggested structure:

```text
student_photos/
├── Class 1/
│   ├── 001.jpg
│   └── 002.jpg
└── Class 2/
    └── 101.png
```

Data conventions:

- Each worksheet in `students.xlsx` represents one class
- Photo folders are grouped by class
- File names should preferably use student IDs
- Supported formats: `.jpg`, `.jpeg`, `.png`, `.bmp`
- The app can generate a template when no student data is found

## Repository Layout

```text
src/ClassroomToolkit.App          WPF UI, startup, windows, and session orchestration
src/ClassroomToolkit.Application  Application use cases and cross-module coordination
src/ClassroomToolkit.Domain       Core rules and business models
src/ClassroomToolkit.Services     Runtime bridges and application services
src/ClassroomToolkit.Infra        Configuration, persistence, and filesystem details
src/ClassroomToolkit.Interop      Win32 / COM / WPS integration boundaries
tests/ClassroomToolkit.Tests      Automated tests
```

## Build and Verification

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

## Documentation

- [Chinese README](./README.md)
- [Teacher Guide](./使用指南.md)
- [Architecture docs](./docs/architecture/)
- [Release checklist](./docs/runbooks/release-checklist.md)
- [Classroom pilot validation runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## Known Limitations

- Windows classroom PCs are the primary target
- Multi-monitor, DPI scaling, projector, and slideshow integration still require on-site validation
- Missing runtimes, permissions, or device drivers may require IT support
- Student rosters, photos, and settings are stored locally and should be backed up appropriately

## License

Released under the MIT License.
