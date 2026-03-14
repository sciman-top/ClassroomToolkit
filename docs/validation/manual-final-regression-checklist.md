# 人工最终回归执行清单（终态冻结前）

最后更新：2026-03-15  
适用范围：`manual-final-regression` 人工门

## 1. 执行前准备

- 构建与自动化基线必须已通过：
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`
- 准备真实教室环境：
  - 双屏或投影（建议 4K + 投影组合）
  - WPS 与 PowerPoint 可正常全屏放映
  - 触控/鼠标/触笔设备可用
- 记录日志目录与截图目录（建议：`docs/validation/evidence/2026-03-15-manual-final-regression/`）

## 2. 核心功能回归

- `PPT 全屏`：放映进入/退出、翻页、批注、焦点恢复
- `WPS 全屏`：放映进入/退出、翻页、批注、焦点恢复
- `图片全屏`：打开/关闭、缩放/平移、前后台切换
- `PDF 全屏跨页`：跨页书写、翻页、回放、刷新一致性
- `白板`：书写/擦除/清空/恢复
- `互切`：图片 / PDF / 白板 / PPT-WPS 任意互切后输入与窗口关系正确

## 3. 窗口与输入关系回归

- 启动器置顶关系稳定
- 工具条置顶关系稳定
- 点名窗口置顶与透明关系正确
- Overlay 激活、焦点、输入穿透符合预期
- ImageManager 激活与前台切换正常

## 4. Ink 与跨页专项回归

- 书写、擦除、翻页、恢复稳定
- 跨页连续书写稳定
- 不依赖“拖动/缩放后才刷新”的补偿行为
- GPU 关闭时 CPU 路径稳定
- 仅在 `CTOOLKIT_USE_GPU_INK_RENDERER=1` 且 `CTOOLKIT_ENABLE_EXPERIMENTAL_GPU_INK=1` 且能力满足时启用 GPU；失败必须回退 CPU

## 5. 结果判定与落档

- 使用模板：`docs/validation/templates/classroom-pilot-acceptance-template.md`
- 产出文件建议：`docs/validation/classroom-pilot-acceptance-YYYY-MM-DD.md`
- 同步更新：
  - `docs/validation/2026-03-06-target-architecture-progress.md`
  - `docs/handover.md`
  - `docs/validation/target-architecture-final-acceptance.md`（结果区）

## 6. 失败回退准则

- 任何阻断级问题（崩溃/卡死/控制失效）直接判定人工门不通过
- 回退依据：`docs/runbooks/migration-rollback-playbook.md`
- 回退后需重新执行第 1 节自动化基线，再重跑人工回归
