# Repository Guidelines

## Project Structure & Module Organization
This repository is a single-file PyQt desktop app with supporting assets.

- 始终使用【简体中文】回答，除非我在指令里明确要求使用其他语言。
- 解释代码、命令行输出、你的思考过程等，所有都尽量用中文讲解。
- 避免自我介绍和客套话，直接进入解决问题。

- 只有在：
  - 需要执行「危险操作」（删除大量文件、强制覆盖等），或
  - 我给的需求明显含糊不清时再停下来向我确认。

- `ClassroomToolkit.py`: main application entry point and all logic.
- `students.xlsx`: default student data template (auto-created/used by the app).
- `student_photos/`: student photo assets referenced by the UI.
- `icon.ico`: application icon.
- `classroom_toolkit.log`: runtime log file (created on first run).

## Build, Test, and Development Commands
This project runs directly from source without a build step.

- `python ClassroomToolkit.py`: start the app.
- `python -m pip install PyQt6`: required GUI dependency.
- Optional features:
  - `python -m pip install pandas openpyxl`: enables roll-call workbook support.
  - `python -m pip install numpy sounddevice`: enables timer alert sounds.
  - `python -m pip install pyttsx3 comtypes pywin32`: enables TTS on Windows.

## Coding Style & Naming Conventions
- Indentation: 4 spaces (PEP 8 style).
- Naming: classes in `CamelCase`, functions/variables in `snake_case`, constants in `UPPER_SNAKE_CASE`.
- No formatter or linter is configured in this repo; keep edits consistent with existing style.

## Testing Guidelines
There are no automated tests in this repository.

- If you add tests, document the framework and the command to run them in this file.
- Prefer deterministic, self-contained tests that do not require GUI interaction.

## Commit & Pull Request Guidelines
No Git history is available in this directory, so no commit message convention can be inferred.

- Use clear, imperative commit messages (e.g., "Fix roll call save path").
- PRs should include a brief description, reproduction steps (if fixing a bug), and screenshots/GIFs for UI changes.

## Configuration & Data Notes
- User data persists to `students.xlsx` and `student_photos/`; avoid destructive changes to these paths.
- Set `CTOOL_NO_STARTUP_DIAG=1` to skip startup diagnostics when debugging launch issues.
