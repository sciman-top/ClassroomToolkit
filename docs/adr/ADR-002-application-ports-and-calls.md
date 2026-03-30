# ADR-002: Application 端口与跨模块调用原则

- 日期: 2026-02-24
- 状态: Accepted

## 决策
- 跨模块编排通过 `Application.UseCases`。
- 外部能力（存储/演示控制）通过 `Application.Abstractions` 定义。
- UI 层不直接编排 Infra 细节。

## 已落地
- Photos 导航迁移到 `Application.UseCases.Photos`。
- RollCall 名册读写迁移到 `Application.UseCases.RollCall`。
