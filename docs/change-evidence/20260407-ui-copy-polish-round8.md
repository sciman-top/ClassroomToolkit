# 20260407 UI Copy Polish Round8

## 依据
- 继续压缩画笔设置页里仍偏长的句尾说明和关闭提示。
- 目标是在不改语义的前提下进一步减少扫读负担。

## 变更
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 将关闭提示收紧为“关闭”。
  - 将基础、工具栏和兼容页说明进一步压短。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若句尾过短影响理解，回退到上一版描述，并无需修改交互逻辑。
