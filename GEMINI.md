# ClassroomToolkit Project Context

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

## Project Overview

**ClassroomToolkit** is a modern Windows Presentation Foundation (WPF) application designed to assist teachers in the classroom. It provides a suite of tools including a roll call system, timer/stopwatch, and screen painting utilities. The application is built using .NET 8.0 and follows a modular architecture.

### Key Features
*   **Roll Call System:** Random student selection, class management, and grouping support.
*   **Timer & Stopwatch:** Countdown timer and stopwatch functionality with visual and audio alerts.
*   **Screen Paint:** Overlay drawing tools for screen annotation.
*   **Remote Control:** Integration with presentation clickers (remote presenters) for controlling the application.
*   **Student Photos:** Display of student photos during roll call.

## Architecture

The solution uses a Clean Architecture-inspired structure, separating concerns across multiple projects:

*   **`src/ClassroomToolkit.App`**: The main WPF application entry point. Contains Views (XAML), ViewModels (MVVM pattern), and UI-specific logic.
*   **`src/ClassroomToolkit.Domain`**: The core domain layer containing business entities, models, and interfaces. It has no dependencies on external frameworks.
*   **`src/ClassroomToolkit.Infra`**: Infrastructure layer responsible for data persistence (e.g., loading student workbooks, saving settings) and external system integration.
*   **`src/ClassroomToolkit.Services`**: Business logic services, such as the `RollCallEngine` and `TimerEngine`.
*   **`src/ClassroomToolkit.Interop`**: Handles native Windows API interactions (e.g., global keyboard hooks for remote presenters).
*   **`tests/ClassroomToolkit.Tests`**: Unit tests for the application.

## Development Environment & Conventions

### Prerequisites
*   .NET 8.0 SDK
*   Windows OS (due to WPF and Win32 interop dependencies)

### Building and Running

1.  **Restore Dependencies:**
    ```bash
    dotnet restore
    ```

2.  **Build the Solution:**
    ```bash
    dotnet build
    ```

3.  **Run the Application:**
    Navigate to the project root and run:
    ```bash
    dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj
    ```

4.  **Run Tests:**
    ```bash
    dotnet test
    ```

### Coding Conventions
*   **Pattern:** The project strictly follows the **MVVM (Model-View-ViewModel)** pattern for the UI layer. Logic should reside in ViewModels, not Code-Behind, unless it's purely view-specific (e.g., window dragging).
*   **Styling:** XAML resources (Colors, Styles, Icons) are centralized in `src/ClassroomToolkit.App/Assets/Styles/`. Always use these static resources instead of hardcoded values.
*   **Dependency Injection:** Services and dependencies are manually instantiated or injected where needed (currently simple instantiation in `App.xaml.cs` or Window constructors).

### Data Storage
*   Student data is stored in Excel/Workbook files (e.g., `students.xlsx`).
*   Photos are loaded from the `student_photos` directory.
*   Application settings are persisted in JSON or similar formats within the user's profile or local directory.

## Directory Structure Highlights

*   `src/ClassroomToolkit.App/Assets/Styles/`: XAML resource dictionaries for theming (Colors.xaml, Icons.xaml, WidgetStyles.xaml).
*   `src/ClassroomToolkit.App/ViewModels/`: ViewModel classes driving the UI logic.
*   `src/ClassroomToolkit.App/Photos/`: Logic for resolving and caching student photo paths.
*   `student_photos/`: Directory containing student images organized by class.
