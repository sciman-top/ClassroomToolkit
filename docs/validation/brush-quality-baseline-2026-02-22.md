# 画笔质量基线记录（2026-02-22）

## 执行命令

```powershell
powershell -ExecutionPolicy Bypass -File scripts/collect-brush-quality-baseline.ps1 -Configuration Debug
```

## 报告位置

- `logs/brush-quality-baseline/20260222_174509/baseline-report.md`

## 结果摘要

- 结论：PASSED
- 总测试：26
- 通过：26
- 失败：0
- 跳过：0
- 总耗时：4905 ms

## 分批结果

| 批次 | 耗时(ms) | 通过/总数 |
| --- | ---: | ---: |
| quality-regression | 1605 | 5/5 |
| stylus-replay | 1563 | 16/16 |
| performance-guard | 1737 | 5/5 |

## 说明

- 本次为 Debug 配置基线，主要用于算法回归与模式切换一致性守护。
- 发布前仍需按项目规则执行 Release 验证与真实课堂设备人工验收。
