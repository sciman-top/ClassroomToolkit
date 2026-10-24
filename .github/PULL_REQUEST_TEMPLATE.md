## 变更背景 / Problem
<!-- 说明本次 PR 解决的具体用户问题、缺陷或文档优化 -->

## 变更内容 / Solution
<!-- 简要列出具体改动的文件与实现方式 -->

## 验证证据 / Verification
<!-- 请根据修改范围提供对应的验证结果（如文档检查、受影响测试或质量门禁执行命令与结果） -->
- [ ] 文档改动：`git diff --check` 通过
- [ ] 代码小改动：受影响单元测试通过
- [ ] 核心/共享改动：运行标准质量门禁 `scripts/quality/run-local-quality-gates.ps1 -Profile standard` 通过

## 兼容性与不变量检查 / Invariant Check
- [ ] 未破坏 `students.xlsx`、`student_photos/` 和 `settings.ini` 的向后兼容
- [ ] Win32 / COM / WPS 异常未冒泡至 UI 层
