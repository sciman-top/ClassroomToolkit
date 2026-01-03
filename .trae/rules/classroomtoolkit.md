# project_rules.md（项目级｜ClassroomToolkit）
WPF 教学工具：点名/计时/画笔白板/PowerPoint&WPS翻页。Windows Only；.NET8 WPF+WinForms互操作；PInvoke；ClosedXML；xUnit。

## 模块边界
App(UI/XAML/输入绘制)；Domain(业务引擎)；Services(策略协调)；Interop(PInvoke/Automation)；Infra(INI/Excel)。Domain 不依赖 Interop。

## 必跑命令（改动后）
`dotnet restore`；`dotnet build ClassroomToolkit.sln`；`dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`；`dotnet run --project src/ClassroomToolkit.App/ClassroomToolkit.App.csproj`（smoke）。跑不了需说明原因+替代验证。

## 数据保护（强约束）
不破坏/覆盖：`students.xlsx`、`student_photos/`；未明确指令不改Excel结构。`settings.ini`写入建议“临时文件→原子替换”。

## 错误处理与降级
外部边界（Excel/语音/Automation/PInvoke/I/O）局部try/catch；关键失败 MessageBox+可执行建议；能降级就降级，但不“假装成功”。

## 调试速记
焦点/穿透：Spy++，核对`GetForegroundWindow()`与`WS_EX_NOACTIVATE/WS_EX_TRANSPARENT`；演示控制：开诊断日志看`PresentationClassifier`与输入发送；性能：WriteableBitmap，高频事件禁重操作，Brush/Pen可冻结则冻结。
