# 功能模块模板（增/删功能的固定触点）

创建：2026-09-05。目的：把"新增/删除一个功能要动哪些地方"固定为可审查的最小触点集合，避免功能代码散落到 App 根目录与跨子系统文件。

## 新增一个功能的 5 个触点

1. **功能目录**：`src/ClassroomToolkit.App/<Feature>/`。窗口、ViewModel、状态与决策逻辑都放这里；跨窗口协调用 `<Feature>Coordinator` 或组合根注册的 orchestrator，单调用点纯函数优先内联为私有方法，不新建微型决策类（见下节约束）。
2. **组合根注册组**：`src/ClassroomToolkit.App/Startup/AppCompositionRoot.cs` 中一段以功能名注释分隔的注册组。窗口生命周期只调用组合根入口。
3. **设置**：`App/Settings/AppSettings.cs` 属性 + `AppSettingsService.Sections.cs` 的 `Apply<Section>Settings`/`Save<Section>Settings` 读写对 + 缺省回退语义测试。
4. **入口**：`MainWindow` 对应 partial 中一个按钮处理/工厂调用；不在 MainWindow 里堆功能实现。
5. **测试**：`tests/ClassroomToolkit.Tests/<Feature>/` 子目录，与 src 功能目录同名映射。

删除功能 = 按上述 5 个触点反向移除；若涉及 `students.xlsx`/`settings.ini`/`student_photos/` 读写格式，遵循 AGENTS.md 数据兼容边界并补迁移或兼容读取说明。

## 范例

- **新增设置项**：自动更新开关（`AppSettings.UpdateAutoCheckEnabled` + `[Update] auto_check_enabled` 区段 + About 开关 + `AutoUpdateBootstrapper` 门控 + `AppSettingsServiceTests` 回退语义测试）。
- **新增功能（较完整）**：`Photos/`（功能目录 + 组合根工厂注册 + MainWindow.Photo 入口 + 独立测试）。

## 决策类碎片化约束（门禁强制）

- 新建 `*Policy`/`*Executor`/`*StateUpdater`/`*Coordinator`/`*Defaults`/`*Thresholds` 文件不得低于 15 行（`scripts/quality/check-hotspot-line-budgets.ps1` 强制）；更小的决策写成消费方私有方法。
- 该门禁的例外走 `scripts/quality/hotspot-microclass-baseline.txt`：只收录 2026-09-05 存量，只减不增；文件删除或长大（≥15 行）后对应条目必须在同一提交修剪，否则门禁失败。
- 退役条件：基线清空后删除基线文件，15 行下限检查自持。
