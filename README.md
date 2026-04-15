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
- [Release prevention checklist](./docs/runbooks/release-prevention-checklist.md)
- [Pilot validation runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## Notes
- Target platform is Windows 10 / 11
- The app is designed to degrade safely when external dependencies fail
- Keep `students.xlsx`, `student_photos/`, and `settings.ini` format-compatible

## License
MIT

## Why this project
- Pain: Repeated manual governance checks and inconsistent project setup across classroom repos.
- Result: Consistent setup, safer changes, and faster validation loops.
- Differentiator: Rule distribution and quality gates are executed as a repeatable workflow.

## Who it is for
- Repository maintainers and teaching operations engineers
- Managing classroom templates, checks, and policy rollouts
- Use this when manual setup or validation starts causing repeated drift

## Quick Start (5 Minutes)
### Prerequisites
- PowerShell 7+
- Git working copy with access to governance scripts

### Run
```bash
powershell -File scripts/doctor.ps1
```

### Expected Output
- HEALTH=GREEN in doctor output
- verify/target checks report PASS

## What you can try first
- Run doctor to validate current governance state
- Run install in plan mode before safe distribution
- Use cycle/autopilot scripts for governed iteration

## FAQ
- Q: Doctor reports FAIL
- A: Run scripts/verify.ps1 first, fix the first failing gate, then rerun doctor

## Limitations
- Designed for governance-managed repositories
- Requires policy and target mappings to be maintained

## Next steps
- docs/
- RELEASE_TEMPLATE.md
- issues/
