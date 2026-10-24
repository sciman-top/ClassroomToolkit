# ClassroomToolkit

<p align="center">
  <img src="./icon.ico" width="88" height="88" alt="ClassroomToolkit Logo" />
</p>

<p align="center">
  <strong>Free & open-source classroom teaching assistant for Windows touchscreens, interactive whiteboards, and school PCs</strong>
</p>

<p align="center">
  <a href="./README.md">中文主页 (Chinese)</a> ·
  <a href="./使用指南.md">教师使用指南 (Teacher Guide)</a> ·
  <a href="https://github.com/sciman-top/ClassroomToolkit/releases/latest">Latest Releases</a> ·
  <a href="#quick-start">Quick Start</a> ·
  <a href="#community">Community</a>
</p>

<p align="center">
  <a href="https://github.com/sciman-top/ClassroomToolkit/releases/latest"><img src="https://img.shields.io/github/v/release/sciman-top/ClassroomToolkit?color=00B4D8&label=Release" alt="Release" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" /></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?logo=windows&logoColor=white" alt="Platform" /></a>
  <img src="https://img.shields.io/badge/Privacy-100%25%20Offline-2A9D8F?logo=safari&logoColor=white" alt="100% Offline" />
  <img src="https://img.shields.io/badge/Crash%20Safe-Fail--Closed-E76F51" alt="Crash Safe" />
  <a href="./LICENSE"><img src="https://img.shields.io/badge/License-MIT-457B9D" alt="License" /></a>
</p>

---

## 🌟 Why ClassroomToolkit?

Teaching in a modern classroom often comes with friction:
- **Heavy commercial teaching suites** (such as Seewo / HiteVision) are bloated, take 20–30+ seconds to boot on low-spec classroom OPS computers, and consume excessive memory;
- **Mandatory cloud accounts and online sign-ins** slow down teachers and create student privacy concerns when rosters are pushed to external clouds;
- **Popups, ads, and update notifications** interrupt active lessons;
- Standalone tools downloaded from the internet frequently **freeze, crash, or lose window focus** whenever PowerPoint enters full-screen slideshow mode.

**ClassroomToolkit was handcrafted by a frontline physics teacher** to solve these exact classroom pain points. It is focused, blazing fast, 100% local, crash-resilient, and purpose-built for classroom large screens:

| Comparison | ClassroomToolkit | Commercial EdTech Suites | Ad-hoc Online Tools |
| :--- | :--- | :--- | :--- |
| **Startup & Footprint** | **Instant launch (<1s)**, minimal memory & CPU usage | Heavy, sluggish startup on older classroom OPS PCs | Inconsistent, frequent memory leaks |
| **Privacy & Network** | **100% Offline-First**, zero cloud telemetry, no accounts | Mandatory cloud accounts, roster data uploaded | Often requires internet or tracking |
| **Ads & Cost** | **Free & Open Source forever**, zero ads, zero popups | Commercial licenses, paywalls, upselling | Bundled ads or unwanted software |
| **Slideshow Integration** | **Automatic detection of PPT & WPS**, smooth drawing & wheel scrolling | Often steals focus, causing blackout or freezing | Hidden or blocked by full-screen slides |
| **Whiteboard Problem-Solving** | **One-click crop of any screen region directly into whiteboard** | Multi-step window switching required | Not supported |
| **Crash Protection** | **Strict Win32/COM isolation & graceful degradation** | Plugin crash often crashes PowerPoint | Easily crashes classroom flow |

---

## 🎯 Key Features

### 1. 🎲 Smart Random Roll-Call
- **Classes & Groups**: Fast class switching; select from the entire class or specific student groups.
- **Large-Screen Legibility & Photos**: Giant dynamic typography legible from the back of the lecture hall; optionally displays student IDs and avatar photos.
- **Offline Text-to-Speech (TTS)**: Reads student names aloud using Windows built-in speech synthesis.
- **Remote Presenter Clicker**: Trigger roll call with wireless presenter remotes (default `Tab` key) while walking freely around the classroom.
- **Duplicate-Free Drawing**: Configurable non-repeat policy with instant reset.

### 2. ⏱️ Classroom Dual-Mode Timers
- **Countdown & Countup**: Countdown for timed quizzes and group work; stopwatch for speeches and activities.
- **Gigantic Typography**: High-contrast digits clearly visible on classroom projectors.
- **Audible Bell Alert**: Plays an attention-grabbing chime when time expires.
- **Quick Presets**: 1 min, 3 min, and 5 min one-tap quick starts.

### 3. ✏️ Screen Annotations & Digital Whiteboard
- **Transparent Screen Overlay**: Draw freely across desktops, web browsers, exam papers, and software windows.
- **Versatile Brush Engines**:
  - **Calligraphy / Brush (毛笔)**: Simulates natural ink blooms and artistic pressure;
  - **Standard Pen**: Responsive, zero-latency everyday writing;
  - **Highlighter**: Translucent stroke for key concepts.
- **Geometric Shapes**: Lines, dashed lines, arrows, dashed arrows, rectangles (outline/filled), ellipses, and triangles.
- **Quick-Select Color Bubbles**: Tap to toggle colors; long-press to customize palette and stroke width.
- **Robust Canvas Tools**: Undo, stroke eraser, bounding-box erase, clear canvas, and one-tap screenshot saving.

### 4. ✂️ Screen Region to Whiteboard (Exam Problem Solver)
- While reviewing exam papers, PDFs, or slides, click **"Capture to Whiteboard"**;
- Drag to select any question or diagram; the cropped region is **instantly imported into a full-screen whiteboard**;
- Choose between classic Blackboard Green, Clean Whiteboard, or Charcoal background for step-by-step mathematical derivation.

### 5. 🖼️ Native High-Performance Image & PDF Presenter
- **Windows Native DirectRenderer**: No heavy 50MB+ third-party PDF binaries; super-fast rendering for large multi-page exam sheets and slides.
- **Intuitive Gestures**: Smooth mouse wheel page-turning, two-finger pinch zoom, drag to pan, and in-place ink annotations.
- **Thumbnail Drawer**: Instant thumbnail overview for jumping between exam pages.

### 6. 📽️ PowerPoint & WPS Presentation Co-pilot
- **Zero Plugin Installation**: Low-level heuristic classifiers detect Microsoft PowerPoint and WPS Presentation slideshow states automatically.
- **Auto Attachment**: Transparent annotation canvas automatically docks to the slideshow viewport and recovers seamlessly if focus is shifted.
- **Bidirectional Page-Turning**: Turn slideshow slides directly with your mouse wheel or arrow keys while remaining in drawing mode.

### 7. 🎈 Edge-Docking Launcher Bubble
- Neatly parks itself at the screen edge, occupying virtually zero classroom screen real estate.
- Expands on a single tap; dismisses quickly on background clicks.
- Sized for fingers on large touchscreens (hit targets ≥ 44px).

### 8. 🎨 Teacher-First UI Design System 2.0
- Three high-contrast themes:
  - **Midnight Teal**: Charcoal slate with teal accents; optimal for projector contrast (recommended default);
  - **Blackboard Green**: Classic dark-green chalkboard style for low-light classrooms;
  - **Light**: Crisp white surface for office prep and daytime use.
- Strict compliance with 4.5:1 accessible contrast ratio.

---

## 🚀 Quick Start <a id="quick-start"></a>

### Step 1: Download

Download from [GitHub Releases](https://github.com/sciman-top/ClassroomToolkit/releases/latest):
- **Standard Installer (`Setup.exe`)**: Smallest download, auto-installs .NET Desktop Runtime if missing, supports automatic background updates.
- **Offline Self-Contained Installer (`Setup.exe`)**: Includes all dependencies for air-gapped school intranets.
- **Portable Edition (`.portable.zip`)**: No installation required. Extract to a USB flash drive or desktop and run `启动.bat`.

### Step 2: Prepare Student Roster (`students.xlsx`)

Locate or create `students.xlsx` in the `data/` directory:
- **One sheet per class** (the sheet name is the class name, e.g. `Class 1A`, `Grade 9`);
- Columns: `学号` (Student ID), `姓名` (Name), `分组` (Group, optional).

```text
students.xlsx
├── Class 1A
│   学号    姓名    分组
│   01     Alice   Group 1
│   02     Bob     Group 2
└── Class 1B
    学号    姓名    分组
    01     Charlie Group 1
```

### Step 3: Add Photos (Optional)

Place avatar photos under `student_photos/<ClassName>/<StudentID>.<ext>`:
```text
student_photos/
├── Class 1A/
│   ├── 01.jpg
│   └── 02.png
└── Class 1B/
    └── 01.jpg
```
*Supported formats: `.jpg`, `.jpeg`, `.png`, `.bmp`.*

---

## ⌨️ Shortcut Cheat Sheet

| Context | Key / Gesture | Action |
| :--- | :--- | :--- |
| **Roll Call Window** | `Space` | Start / Stop random roll call |
| **Roll Call Window** | `Tab` (Customizable) | **Wireless presenter clicker roll call** |
| **Pen Overlay** | `Esc` | Exit drawing mode |
| **Pen Toolbar** | Click Color Bubble | Select brush color |
| **Pen Toolbar** | Long-press Color / Shape | Open detailed palette / shape menu |
| **Image / PDF** | Mouse Wheel | Previous / Next page |
| **Image / PDF** | Pinch / Drag | Zoom in / Pan around details |
| **Slideshow** | Mouse Wheel | Advance / Rewind slides while in drawing mode |

---

## 🛡️ Reliability & Data Safety

- **Fail-Closed Protection**: If a student roster or config file is damaged during an unexpected power outage, the application **never overwrites corrupt files** with empty templates, and automatically creates timestamped backups before migrations;
- **Resilient Fault Isolation**: Win32, COM, UIAutomation, and presentation hooks run within guarded sandboxes; external failures trigger graceful degradation rather than crashing the classroom session;
- **100% Local Storage**: All roster details, photos, chalkboard captures, and settings remain strictly on your local disk.

---

## 💻 Developer & Tech Stack

ClassroomToolkit is built with modern **.NET 10** and **WPF** on Windows, adhering to clean architecture and modular monolith principles:

```text
src/ClassroomToolkit.App          WPF UI, composition root, window topology & orchestration
src/ClassroomToolkit.Application  Use cases, port definitions, and cross-domain coordinators
src/ClassroomToolkit.Domain       Pure entities, roll-call algorithms, contracts & invariants
src/ClassroomToolkit.Services     Runtime bridges, input devices, speech synthesizer
src/ClassroomToolkit.Infra        Workbook codecs, configuration stores, SQLite adapters
src/ClassroomToolkit.Interop      Win32 / COM / WPS presentation classifiers & fault containment
tests/ClassroomToolkit.Tests      3,000+ automated regression & architecture dependency tests
```

### Build & Run

Prerequisites: Windows 10 22H2+ / Windows 11, [.NET 10 SDK](https://dotnet.microsoft.com/).

```powershell
git clone https://github.com/sciman-top/ClassroomToolkit.git
cd ClassroomToolkit

dotnet restore
dotnet build ClassroomToolkit.sln -c Debug
dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
```

### Quality Gate

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

---

## 🤝 Author & Community <a id="community"></a>

ClassroomToolkit is created and maintained by **Yaoqiang Wang (广州番禺王耀强)**, a frontline middle school physics teacher in Guangzhou, dedicated to empowering fellow teachers with clean, fast, and dependable digital tools.

- **WeChat Official Account**: `sciman逸居`
- **Teacher Exchange QQ Group**: `323728546`
- **Zhihu Column**: [sciman on Zhihu](https://www.zhihu.com/people/sciman/columns)
- **Feedback & Issues**: Feel free to submit bug reports or feature requests via [GitHub Issues](https://github.com/sciman-top/ClassroomToolkit/issues)!

If ClassroomToolkit makes your teaching easier, please give it a ⭐️ **Star** and share it with your colleagues!

---

## 📄 License

Distributed under the [MIT License](./LICENSE).
