# ClassroomToolkit

> Windows classroom toolkit for roll call, timers, annotation, image/PDF presentation, and PowerPoint/WPS slideshow control.

## Overview
- Built for live classroom use on Windows desktop machines
- Focuses on local reliability instead of cloud workflows
- Keeps student roster, photos, and settings in local files

## Main Capabilities
- Roll call, student photo display, and voice announcement
- Countdown, stopwatch, and classroom timing tools
- Whiteboard and screen annotation with pen, touch, or stylus
- Image and PDF presentation with paging, zooming, and panning
- PowerPoint and WPS slideshow support with overlay annotation
- Floating launcher for fast tool switching

## Quick Start
### For Teachers
1. Download a release from GitHub Releases.
2. Extract the package and run `sciman Classroom Toolkit.exe`.
3. Confirm the launcher opens, then test roll call and presentation tools.

### For Developers
```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

## Local Data
- `students.xlsx`: class roster workbook
- `student_photos/`: photo directory grouped by class
- `settings.ini`: local settings file

Supported photo formats:
- `.jpg`
- `.jpeg`
- `.png`
- `.bmp`

## Repository Map
- `src/ClassroomToolkit.App`: WPF UI, startup, and window orchestration
- `src/ClassroomToolkit.Application`: application use cases
- `src/ClassroomToolkit.Domain`: core rules and models
- `src/ClassroomToolkit.Services`: runtime bridges and orchestration
- `src/ClassroomToolkit.Interop`: Win32 / COM / WPS boundaries
- `tests/ClassroomToolkit.Tests`: automated tests

## Verification
```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

## Documentation
- [English README](./README.en.md)
- [Teacher Guide](./使用指南.md)
- [Architecture docs](./docs/architecture/)
- [Release checklist](./docs/runbooks/release-checklist.md)
- [Pilot validation runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## Notes
- Target platform is Windows 10 / 11
- The app is designed to degrade safely when external dependencies fail
- Keep `students.xlsx`, `student_photos/`, and `settings.ini` format-compatible

## License
MIT
