# 20260407 UI Copy Polish Round5

## 依据
- 继续压缩剩余诊断和设置页文案，优先去掉上下文里已经可省略的限定词。
- 保持含义不变，只收紧表述。

## 变更
- `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
  - 将“恢复启动提示”收紧为“恢复提示”。
- `tests/ClassroomToolkit.Tests/App/UiCopyContractTests.cs`
  - 同步更新诊断页按钮文案契约。

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
- 若按钮语义不够明确，恢复为“恢复启动提示”，并同步回退测试断言。
