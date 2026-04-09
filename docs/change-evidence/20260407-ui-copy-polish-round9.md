# 20260407 UI Copy Polish Round9

## 依据
- 继续压缩画笔设置页里仍偏长的术语说明与高级项开关文案。
- 目标是保留准确性，同时减少阅读长度。

## 变更
- `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml`
  - 将 `低不透明度弱化阈值` 收紧为 `低不透明度阈值`。
  - 将 `显示高级兼容与排障项` 收紧为 `显示高级项`。

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
- 若用户反馈“高级项”不够明确，回退为“显示高级兼容与排障项”。
