# ClassroomToolkit Visual Design System 2.0 交付包

版本：2.0 Draft / 2026-08-29

本包用于指导 Codex、zcode/GLM-5.3-Flash 或人工开发者，对 ClassroomToolkit 进行统一的 WPF UI 改版。

## 目标

ClassroomToolkit 是面向 Windows 教室电脑的课堂工具箱，核心场景包括：

- 随机点名与课堂互动
- 倒计时、正计时与活动计时
- 触屏/手写屏/数位板/鼠标的屏幕批注
- 图片与 PDF 全屏讲解
- PowerPoint / WPS 放映导航与叠加批注
- 悬浮启动器和悬浮工具栏

UI 改版必须服务于“课堂中一眼可见、一点即用、低干扰、长时间舒适”，不能为了视觉效果牺牲稳定性、触控性、投影可读性和窗口层级行为。

## 主题策略

正式首发建议采用 **1 个品牌主主题 + 2 个实用主题**：

1. `MidnightTeal`：课堂深色（推荐、默认、品牌基线）
2. `Blackboard`：黑板护眼
3. `Light`：明亮专业

暂不首发蓝紫“极光”主题。未来可作为个性主题增加，但不得改变组件结构和颜色语义。

## 文件说明

- `01_Visual_Design_System_v2.0.md`：总体视觉规范与设计原则
- `02_Component_Specification_v2.0.md`：控件级规范
- `03_Theme_Tokens_v2.json`：三套主题的语义颜色 Token
- `04_WPF_ResourceDictionary_Template.xaml`：WPF 资源字典骨架
- `05_Theme_Architecture_and_Switching.md`：主题切换架构
- `07_UI_QA_Acceptance_Checklist.md`：视觉/交互/课堂验收清单
- `11_Component_State_Matrix.csv`：核心组件状态矩阵
- `12_Implementation_Decisions.md`：必须遵守/禁止事项的简版决策记录

一次性执行文件（改版实施计划、Codex/Zcode 生产提示词、审查修复提示词）已随改版落地退役，需要时从 Git 历史查询。

## 推荐使用方式

不要让 Agent “看着效果图自由发挥”。

推荐流程：

1. 把整个本目录放进仓库，例如 `docs/ui-v2/`
2. 先让 Agent 阅读 `00`、`01`、`02`、`05`、`06`
3. 使用对应的生产提示词启动任务
4. Agent 必须先审计现有 ResourceDictionary/XAML/窗口，再开始修改
5. 所有新样式先落入统一资源体系
6. 再迁移所有窗口，禁止各窗口单独写颜色/圆角/按钮模板
7. 最后使用 `07` 和 `10` 做第二轮独立验收

**关键原则：一次任务可以覆盖整个项目，但执行必须分阶段，不允许“一次性盲改所有 XAML”。**
