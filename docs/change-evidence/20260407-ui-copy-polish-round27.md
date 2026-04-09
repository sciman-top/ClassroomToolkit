# 20260407 UI Copy Polish Round27

## 依据
- 继续压缩系统诊断、兼容诊断结果和提示页里的可见文案。
- 目标是减少冗余说明，同时保留教师可直接执行的修复建议。

## 变更
- `src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs`
  - 将可写目录、学生数据、照片目录、目录冲突和语音提示进一步收短。
- `src/ClassroomToolkit.App/Diagnostics/DiagnosticsResult.cs`
  - 将兼容问题摘要收短。
- `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml.cs`
  - 将重置提示和恢复提示说明收短。
- `tests/ClassroomToolkit.Tests/App/SystemDiagnosticsCopyContractTests.cs`
  - 新增系统诊断文案契约。

## 验证
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~SystemDiagnosticsCopyContractTests|FullyQualifiedName~DiagnosticsDialogContractTests|FullyQualifiedName~StartupCompatibilityAutoRemediationPolicyTests|FullyQualifiedName~StartupCompatibilityWarningCopyContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## 结果
- build: PASS
- test: PASS
- contract/invariant: PASS
- hotspot: PASS

## 回滚
- 若诊断建议过短影响排查，回退到上一版表述。
