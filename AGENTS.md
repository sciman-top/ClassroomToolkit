# Repository Guidelines

# Output Language Policy

## 用户可见输出语言
- 所有最终输出、分析说明、代码解释、设计决策、文档、列表、任务进度更新**必须使用中文**
- 技术术语保持英文原文（如 WPF, Grid, DependencyProperty），避免歧义
- 允许在代码注释中使用中文

## 模型内部推理（不可见内容）
- 模型的内部思考、推理链、规划语句（chain-of-thought / reasoning）无需翻译成中文
- 若 reasoning 被展示，则无需更改英文形式
- **模型在开始执行前必须用中文复述任务理解**（用于同步上下文）

## 上下文连续性
每次输出前执行以下步骤：
1. 用中文总结当前任务理解
2. 输出结果（中文）
3. 若 reasoning 被展示，无需翻译，保持英文

## Project Structure & Module Organization
This repository is a single-file PyQt desktop app with supporting assets.

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
