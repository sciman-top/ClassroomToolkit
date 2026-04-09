# 20260407 UI Copy Polish Round14

## 依据
- 继续压缩启动兼容提示的底部说明。
- 保持意思不变，减少重复表述。

## 变更
- `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml`
  - 将提示语收紧为更短的版本。
- `tests/ClassroomToolkit.Tests/App/StartupCompatibilityWarningDialogContractTests.cs`
  - 同步更新契约断言。

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
- 若新提示读起来太省略，回退到上一版完整句。
