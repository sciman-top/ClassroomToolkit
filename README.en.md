# sciman Classroom Toolkit

[中文](./README.md) | English

> A Windows-first classroom toolkit focused on roll call, timers, on-screen annotation, image/PDF presentation, and slide-show control for PowerPoint and WPS.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

sciman Classroom Toolkit is not trying to be a full school platform. It is a set of local teaching utilities designed for live classroom work: fewer window switches, less setup friction, and faster access to the tools teachers actually use during a lesson.

## What It Is For

- Random roll call and in-class interaction
- Countdowns, stopwatches, and timed activities
- Screen annotation with touchscreens, pen displays, or stylus tablets
- Explaining worksheets, images, and PDF handouts on a projector
- Annotating and navigating PowerPoint / WPS slide shows

## Core Features

| Module | Capabilities | Typical use |
|------|------|------|
| Roll Call / Timer | Random selection, student photo display, voice announcement, countdown / stopwatch, remote-key trigger | Participation, time-boxed tasks, group presentations |
| Pen / Whiteboard | Screen annotation, regular pen / brush / eraser, color and stroke settings, ink save and replay | Solving problems, marking key points, live explanation |
| Images / PDF | Full-screen viewing, paging, zooming, panning, annotation overlay | Worksheet review, image analysis, PDF lecture notes |
| PPT / WPS | Slide-show detection, page navigation, wheel mapping, overlay annotation | Lesson presentation and slide-based teaching |
| Launcher | Floating entry point for all tools | Fast switching during class |

## Project Scope

This repository intentionally does not cover:

- School administration, grading, or assignment workflows
- Mandatory cloud accounts, server deployment, or online sync
- Breaking changes to `students.xlsx`, `student_photos/`, or `settings.ini`
- Cross-platform support beyond Windows desktop environments

The goal is to keep the product reliable and practical for real classrooms instead of turning it into a broad platform.

## Requirements

- Windows 10 or Windows 11
- 1920x1080 or higher is recommended
- End users should normally use packaged releases
- Developers should install the [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional hardware: touch screen, pen tablet, presentation remote, projector / external display

## Quick Start

### For Teachers

Download a release package from [GitHub Releases](https://github.com/sciman-top/ClassroomToolkit/releases), extract it, and run `sciman Classroom Toolkit.exe`.

Recommended first-run checks:

1. Confirm the floating launcher appears
2. Confirm the roll-call window can load classes and students
3. Open an image or PDF
4. Verify annotation and page navigation in a PPT / WPS slide show

### For Developers

Run from the repository root:

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

## Release Variants

- GitHub edition: source code and documentation repository
- Standard edition: does not bundle `.NET Desktop Runtime 10 x64`
- Offline edition: ships with prerequisite runtime installers for restricted environments

## Local Classroom Data

The application reads two local resources:

- `students.xlsx`: student roster workbook
- `student_photos/`: student photo directory

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

See the [Teacher Guide](./使用指南.md) for the classroom workflow in Chinese.

## Documentation

- [Chinese README](./README.md)
- [Teacher Guide](./使用指南.md)
- [Architecture Docs](./docs/architecture/)
- [Release Prevention Checklist](./docs/runbooks/release-prevention-checklist.md)
- [Classroom Pilot Validation Runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## Build and Verification

Common commands:

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

Repository layout:

```text
src/ClassroomToolkit.App          WPF UI, startup, windows, and session orchestration
src/ClassroomToolkit.Application  Application use cases and cross-module coordination
src/ClassroomToolkit.Domain       Core rules and business models
src/ClassroomToolkit.Services     Runtime bridges and application services
src/ClassroomToolkit.Infra        Configuration, persistence, and filesystem details
src/ClassroomToolkit.Interop      Win32 / COM / WPS integration boundaries
tests/ClassroomToolkit.Tests      Automated tests
```

## Known Limitations

- Windows classroom PCs are the primary target
- Multi-monitor, DPI scaling, projector, and slideshow integration still require on-site validation
- Missing runtimes, permissions, or device drivers may require IT support
- Student rosters, photos, and settings are stored locally and should be backed up appropriately

## Feedback and Contribution

- Issues: <https://github.com/sciman-top/ClassroomToolkit/issues>
- Pull requests should pass build and test validation before submission
- Contributors should review the architecture and governance documents first

## License

Released under the [MIT License](./LICENSE).
