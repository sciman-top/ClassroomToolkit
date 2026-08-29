# ClassroomToolkit

[中文](./README.md) | English

> A free, open-source classroom toolkit for Windows teaching PCs: random roll call, timers, screen annotation, image/PDF presentation, and PowerPoint / WPS slideshow control — in one local app.

[![Release](https://img.shields.io/github/v/release/sciman-top/ClassroomToolkit)](https://github.com/sciman-top/ClassroomToolkit/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](./LICENSE)

## What is this

A Windows desktop app built for K-12 teachers. It stays focused on the highest-frequency classroom actions:

- 🎲 **Random roll call** — pick students by class or group, with student IDs, photos, voice announcement, and remote triggering via a presentation clicker
- ⏱️ **Timers** — countdown, stopwatch, and activity timing with an audible alert
- ✏️ **Screen annotation & whiteboard** — works with touch displays, pen displays, drawing tablets, and a plain mouse; capture a screen region onto the board, with undo, save, and replay of ink
- 🖼️ **Image / PDF presentation** — full-screen display, wheel paging, zoom and pan with live annotation; PDF renders through the Windows-native `Windows.Data.Pdf` API with no third-party native engine
- 📽️ **PPT / WPS slideshow control** — detects running slideshows and annotates directly on slides
- 🚀 **Floating launcher** — a small on-screen button for switching tools mid-lesson

Highlights:

- **Fully local** — rosters (`students.xlsx`), photos, and settings are local files. No cloud account, no data upload
- **Safe with your data** — corrupt roster or settings files fail closed; originals are never overwritten by templates or defaults, and format migrations back up first
- **Built for aging classroom PCs** — standard installer, offline self-contained installer, and a no-install portable build; external-device failures degrade gracefully instead of crashing a lesson

Out of scope: school administration or grading workflows, online collaboration, and anything beyond the Windows desktop.

## Download

Grab the latest build from [GitHub Releases](https://github.com/sciman-top/ClassroomToolkit/releases/latest). All three deliverables have the same features — pick by environment:

| Deliverable | Best for |
|-------------|----------|
| `Setup.exe` (standard, framework-dependent) | Connected classroom PCs; installs the .NET Desktop Runtime on demand |
| `Setup.exe` (offline, self-contained) | Air-gapped networks, bulk installation, or PCs without the runtime |
| `ClassroomToolkit-<version>-portable.zip` | Temporary devices, USB drives, or no-install scenarios; extract and run `启动.bat` |

Installers update in-app (a downloaded update applies on next launch). The portable build only checks for newer releases and opens the download page — it never replaces files automatically. Requires Windows 10 / 11.

The [Teacher Guide](./使用指南.md) is written in Chinese and covers daily classroom usage, checklists, and troubleshooting.

## Where data lives

- `students.xlsx`: roster workbook — one worksheet per class (recommended columns: student ID, name, group)
- `student_photos/`: photos grouped by class folders, file names by student ID; `.jpg` / `.jpeg` / `.png` / `.bmp`
- Installed builds keep data under `%LOCALAPPDATA%\ClassroomToolkit\data`; updates never overwrite classroom data

## For developers

Stack: WPF (.NET 10), layered as App / Application / Domain / Services / Infra / Interop, with 3000+ automated tests and a fixed quality-gate order (`build -> test -> contract/invariant -> hotspot`).

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

Issues and PRs are welcome — see [CONTRIBUTING](./CONTRIBUTING.md).

## Documentation

- [Chinese README](./README.md)
- [Teacher Guide (Chinese)](./使用指南.md)
- [Documentation index](./docs/README.md)
- [Project status snapshot (Chinese)](./docs/project-status.md)
- [Current handover](./docs/handover.md)
- [Tech debt and stability backlog](./docs/tech-debt-backlog.md)
- [Release checklist](./docs/runbooks/release-checklist.md)
- [Classroom pilot validation runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)
- [Security policy](./SECURITY.md) · [Contributing](./CONTRIBUTING.md)

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
docs/                            Architecture, acceptance, selected high-risk evidence, and runbooks
```

## Build and Verification

The fixed delivery gate order is `build -> test -> contract/invariant -> hotspot`:

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate!=CoreContract&Gate!=Performance"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate=CoreContract"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

An aggregate gate is also available: `scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`. Use `quick` only for fast feedback; `standard` closes shared or high-risk seams; use `-Profile full` before releases or after dependency changes (performance budgets, vulnerability and `latest-all` analyzer audits). Documentation-only changes need at least `git diff --check`.

If you are continuing from the current main branch, read [docs/handover.md](./docs/handover.md) first. Passing local gates is not a substitute for on-site classroom validation.

## Known Limitations and Release Boundary

- Windows classroom PCs and touch displays remain the primary target
- Multi-monitor, DPI scaling, projection, real classroom-PDF visuals, and PPT / WPS slideshow integration still deserve on-site validation
- Missing runtimes, permissions, or device drivers may require school IT support
- Student rosters, photos, and settings are local files and should be backed up appropriately

## License

MIT
