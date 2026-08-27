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

## Current Status (2026-08-27)

Recent work has focused on high-frequency classroom flows and touch-first stability:

- In full-screen image / PDF mode, the board button now offers `capture to board / plain board / colored board` instead of jumping straight into board mode.
- The 3 quick-brush slots now keep independent sizes. Tapping the same quick brush again opens color and 3 size choices.
- Undo for image / PDF annotation now restores runtime history, cache, and persistence state, so ink does not disappear again after pan / zoom.
- Student photo overlay, toolbar, roll-call window, and launcher z-order behavior has been hardened, especially on first show and re-show.
- Paint settings dialog construction is now guarded against initialization-time null-reference crashes.
- JSON settings now treat schema corruption, file locks, and transient I/O failures as unsafe-to-overwrite states. Saving resumes only after an explicit successful reload, preserving unknown sections and keys.
- A corrupt `students.xlsx` is no longer replaced by the sample template, and later state saves are blocked. Valid legacy normalization first creates a SHA-256-deduplicated byte-for-byte backup.
- PDF rendering has moved from `PdfiumViewer.Core 1.0.4` and the 2018 native PDFium build to the Windows-provided `Windows.Data.Pdf` API. Automated coverage now includes corrupt-file degradation, oversized-page memory limits, 128-page metadata, black/white visual content, and 96/144-DPI pixel dimensions.
- Shared atomic writes no longer fall back to `File.Copy(overwrite: true)`; unsupported `File.Replace` environments use a same-directory overwrite move, and the single-caller fallback policy was removed.
- Source-string assertions for ink diagnostic text and workbook atomic-write wiring were retired; existing behavior tests continue to cover corrupt reads, locked files, WAL recovery, and temp-file cleanup.
- WPS hook stop/dispose invalidation, intercept gating, and subscriber isolation now use deterministic queued-work behavior tests, retiring four source-string assertions.
- Legacy INI files are now backed up once, by content hash, immediately before a migration is persisted. Read-only loads no longer create duplicate backups, and backup failure blocks overwrite.
- Four one-expression Paint policies and their implementation-mirroring tests were inlined and removed. Wall-clock brush microbenchmarks now run in full/focused verification instead of standard.
- The dependency graph no longer carries the unused SourceGear native SQLite implementation or duplicate test pins; .NET 10 packages are on 10.0.11 and the Test SDK is on 18.9.0.
- The release chain now produces four explicit deliverables: standard installer, offline installer, green portable package, and public source archive. The portable package only checks official GitHub releases and opens the download page; it never replaces files automatically.
- The aggregate release entry point builds under `.staging/<version>` and leaves only installers, the portable ZIP, the source ZIP, and manifests in the final version directory. Superseded candidates and historical validation output are archived under `artifacts/archive/legacy-outputs/`.

Local verification after the current closeout:

- `dotnet build ClassroomToolkit.sln -c Release`: passed, 0 warnings / 0 errors
- full Release stable tests (excluding core contracts and including performance budgets): passed, 3024/3024
- contract / invariant: passed, 29/29
- `latest-all` analyzer: 0 diagnostics; dependency vulnerabilities: 0
- Current code blocker: none
  - Roster workbook and JSON settings read failures now fail closed, preserving originals instead of replacing them with templates or defaults
  - PDF rendering no longer ships a third-party native engine; repository verification is not treated as acceptance of real lesson PDFs, DPI scaling, projection, or classroom visuals

For more context:

- [Documentation index](./docs/README.md)
- [Current handover](./docs/handover.md)
- [High-risk change evidence](./docs/change-evidence/)

## Quick Start

### For Teachers

1. Download the required deliverable from [GitHub Releases](https://github.com/sciman-top/ClassroomToolkit/releases).
2. Choose `standard` for connected classroom PCs; choose `offline` for restricted networks, bulk installation, or PCs without the required runtime; choose the `portable` green package for temporary devices, USB drives, or computers where you do not want to install anything.
3. After installing `standard` / `offline`, confirm that the floating launcher appears, then verify roll call, image / PDF viewing, board entry, and PPT / WPS annotation. For `portable`, extract the ZIP and run the root `启动.bat`.

Both installers provide the same classroom features. `standard` is framework-dependent and installs the .NET Desktop Runtime when needed; `offline` is self-contained. They use separate update channels and apply a downloaded update on the next launch. The `portable` package is self-contained: extract it, run the root `启动.bat`, and keep data beside it in `data/`. It checks official GitHub releases and opens the download page when a newer version is found, but never replaces files automatically. The matching `ClassroomToolkit-Source-<version>.zip` is published separately and is not installed on teacher PCs.

Daily classroom usage is documented in the [Teacher Guide](./使用指南.md).

GitHub Release naming: `*-Setup.exe` files are installers, `ClassroomToolkit-<version>-portable.zip` is the green portable package, and `ClassroomToolkit-Source-<version>.zip` is the public source archive. Private migration packages are never published in public Releases.

### For Developers

```powershell
dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

To prepare release packages, prefer the built-in scripts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Release -Profile full
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/prepare-release-artifacts.ps1 -Version <version> -PackageMode all -Configuration Release -EnsureLatestRuntime
```

After the aggregate script succeeds, `artifacts/release/<version>/` is the upload-ready directory. `.staging/` is retained only after a failure for diagnosis and is cleaned after success. Superseded candidates, old logs, and old performance reports live under `artifacts/archive/legacy-outputs/` and are not public Release assets.

The local `artifacts/` layout is fixed: `release/<version>/` contains final delivery only; `evidence/quality/current/`, `evidence/tests/current/`, `evidence/validation/current/`, and `evidence/release-preflight/current/` contain the latest gate evidence; `private-migration/` is reserved for private migration packages; and `archive/legacy-outputs/` contains recoverable historical byproducts. Repeated gates overwrite stable `current` filenames instead of accumulating timestamped files. Failed release staging is retained only under `release/.staging/<version>/` for diagnosis.

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
- The app can generate a template when `students.xlsx` is missing; an existing corrupt file is preserved and reported as a load failure
- Any format change must preserve compatibility with existing classroom machines and files
- Development runs continue to use solution-root data. Installed builds use `%LOCALAPPDATA%\ClassroomToolkit\data`; on first launch they copy legacy roster/photos only when the persistent target does not exist, so an update cannot replace classroom data.

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

The repository also provides an aggregate quality gate:

```powershell
powershell -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

For routine changes, run the affected tests and a build. Use `quick` only for fast feedback. `standard` closes shared or high-risk seams without wall-clock microbenchmarks; brush-performance changes should run `BrushPerformanceGuardTests` directly. Use `full` before releases or after dependency changes; it includes the performance budget plus vulnerability, update, and `latest-all` analyzer audits.

For documentation-only changes, run at least:

```powershell
git diff --check
```

If you are continuing from the current main branch, read [docs/handover.md](./docs/handover.md) first. The counts above are only the repository snapshot after this simplification; release still needs classroom-site validation.

## Documentation

- [Chinese README](./README.md)
- [Teacher Guide](./使用指南.md)
- [Documentation index](./docs/README.md)
- [Current handover](./docs/handover.md)
- [Tech debt and stability backlog](./docs/tech-debt-backlog.md)
- [High-risk change evidence](./docs/change-evidence/)
- [Release checklist](./docs/runbooks/release-checklist.md)
- [Classroom pilot validation runbook](./docs/runbooks/classroom-pilot-validation-runbook.md)

## Known Limitations and Release Boundary

- Windows classroom PCs remain the primary target
- Multi-monitor, DPI scaling, projection, real classroom-PDF visuals, and PPT / WPS slideshow integration still require on-site validation
- Missing runtimes, permissions, or device drivers may require school IT support
- Student rosters, photos, and settings are local files and should be backed up appropriately
- Code gates are currently green; a release baseline should still include on-site checks for multi-monitor, DPI, projector, PPT / WPS, and student-photo overlay behavior

## License

MIT
