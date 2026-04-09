# 20260407 UI Copy Polish Round10

## 依据
- 继续压缩资源管理页里仍可省略的辅助说明。
- 保持含义不变，减少扫读长度。

## 变更
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
  - 将收藏提示收紧为“取消收藏，保留最近”。
  - 将底部说明收紧为“管理文件、PDF、图片。”。

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
- 若“保留最近”或底部说明不够明确，回退到上一版表述并保持现有交互不变。
