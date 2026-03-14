# Architecture Baseline 2026-02-24（历史基线）

状态：historical  
最后更新：2026-03-07

- 本文档记录早期迁移阶段的验证门禁（当时约 319 tests）。
- 当前主线验证基线已切换到 2026-03-07 的终态重构周期。

当前建议门禁命令：

- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPage|FullyQualifiedName~Windowing|FullyQualifiedName~Session|FullyQualifiedName~Launcher"`

最近一次全量结果：

- Debug：1249/1249 通过
