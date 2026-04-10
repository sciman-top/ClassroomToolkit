# 2026-04-10 Full Governance v2 - Phase 3 Performance And Latency Baseline

## 1) Objective

- 建立可重复执行的性能基线证据，作为后续优化回归对照。
- 优先使用仓内现有脚本，避免临时一次性采样方案。

## 2) Executed Commands

1. `powershell -File scripts/collect-brush-telemetry-report.ps1 -Configuration Debug -SkipRestore -SkipBuild`
   - result: PASS
   - output_dir: `logs/brush-telemetry-report/20260410_184629`

2. `powershell -File scripts/validation/collect-pilot-metrics.ps1 -LogRoot logs -WindowMinutes 30`
   - result: PASS
   - output_file: `logs/validation/pilot-metrics-20260410_184636.md`

## 3) Baseline Snapshot

source: `logs/brush-telemetry-report/20260410_184629/telemetry-report.md`

- preset: `CalligraphyClarity`
  - `dt p95(ms)`: `0.007`
  - `alloc p95(bytes)`: `0`
  - `raw p95`: `123.0`
  - `resampled p95`: `129.0`
- preset: `CalligraphyInkFeel`
  - `dt p95(ms)`: `0.006`
  - `alloc p95(bytes)`: `0`
  - `raw p95`: `123.0`
  - `resampled p95`: `98.0`

source: `logs/validation/pilot-metrics-20260410_184636.md`

- `ErrorEvents`: `0`
- `TelemetrySamples`: `0`
- `GCPercentTimeInGc`: `n/a`（未检测到目标进程计数）

## 4) Interpretation

- 画笔遥测快照链路可用，后续可按同脚本稳定复测。
- 现场指标脚本可运行，但当前 `logs` 下无输入遥测样本，需在真实课堂流量或仿真回放后再次采集。

## 5) Next Actions

1. 接入回放场景后复跑 `collect-pilot-metrics.ps1`，补充输入延迟样本。
2. 在 CI 或 nightly 中固定执行 brush telemetry 脚本，形成趋势图。
3. 将 `dt p95/alloc p95` 纳入 Phase 3 优化验收阈值对照。

