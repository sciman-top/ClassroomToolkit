# 人工最终回归清单

## 自动化前置

发布候选冻结后只运行一次：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full -Configuration Release
```

不要再额外重复 Debug/Release 全量测试或 contract 子集。

## 现场环境

- 双屏或投影，覆盖常见 DPI 缩放。
- PowerPoint 与 WPS 全屏放映。
- 触控、鼠标、触笔/数位板与翻页笔按实际设备覆盖。
- 使用脱敏或测试名单与照片；不要修改教师真实数据。

## 课堂主链

- 启动、配置/名单加载、点名、计时。
- 图片/PDF 打开、缩放、平移、翻页、批注、撤销、跨页恢复。
- 白板书写、擦除、清空、恢复。
- PPT/WPS 进入/退出、翻页、批注、焦点恢复。
- Overlay、工具条、点名、启动器和图片管理器的置顶、穿透与关闭关系。
- 外部依赖不可用时有降级、无崩溃、无长时间卡死。

## 结果

- 使用 `docs/validation/templates/classroom-pilot-acceptance-template.md`。
- 任何崩溃、卡死、输入失效、数据破坏或无法恢复均阻断发布。
- 代码门禁通过只证明 `repo_verified`；完成现场清单后才可报告 `live_accepted`。
