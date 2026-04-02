# 变更证据：20260402 兼容性增强 Phase6（矩阵脚本 JSON 与失败码）

当前落点=增强矩阵报告脚本 CI 能力：JSON 输出 + 预检失败非零退出码
目标归宿=支持 CI/自动化系统直接消费矩阵执行结果并可靠判定失败
风险等级=低

执行命令=powershell -File scripts/validation/run-compatibility-matrix-report.ps1 -Configuration Debug -MatrixId BL-02 -PresentationVendor PowerPoint -PresentationEdition "LTSC" -PresentationVersion "Unknown" -PresentationProcessName "POWERPNT" -PresentationClassSignature "pptviewwndclass" -PresentationArch x64 -PrivilegeMatch Yes -RunPreflight -EmitJson -FailOnPreflightFailure; powershell -File scripts/validation/build-compatibility-report-index.ps1; powershell -File scripts/validation/update-compatibility-baseline.ps1; codex status; codex --version; codex --help
验证证据=BL-02 markdown+json report generated; preflight ALL PASS (build/test3085/contract24/hotspot PASS); baseline/index updated with BL-02; codex --version=codex-cli 0.118.0; codex --help printed usage

platform_na=cmd: codex status | reason: stdin is not a terminal（非交互终端限制） | alternative_verification: codex --version + codex --help + active_rule_path=E:/CODE/ClassroomToolkit/AGENTS.md | evidence_link: docs/change-evidence/20260402-compatibility-hardening-phase6-json-and-exit-code.md | expires_at: 2026-05-02

变更文件=
- scripts/validation/run-compatibility-matrix-report.ps1
- docs/compatibility/reports/20260402-210426-SCIMAN-HOME-BL-02.md
- docs/compatibility/reports/20260402-210426-SCIMAN-HOME-BL-02.json
- docs/compatibility/reports/index.md
- docs/compatibility/matrix-baseline-2026Q2.md

回滚动作=git restore --source=HEAD~1 -- scripts/validation/run-compatibility-matrix-report.ps1 docs/compatibility/reports/20260402-210426-SCIMAN-HOME-BL-02.md docs/compatibility/reports/20260402-210426-SCIMAN-HOME-BL-02.json docs/compatibility/reports/index.md docs/compatibility/matrix-baseline-2026Q2.md docs/change-evidence/20260402-compatibility-hardening-phase6-json-and-exit-code.md
