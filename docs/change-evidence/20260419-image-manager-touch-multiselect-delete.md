# 20260419-image-manager-touch-multiselect-delete

- rule_id: R1,R2,R6,R8
- risk_level: medium
- topic: 资源管理窗口触控多选删除与打开行为调整
- scope:
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.State.cs
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.Lifecycle.cs

## commands
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`

## key_output
- build: success, warnings=0, errors=0
- test: passed=3273, failed=0, skipped=0
- contract/invariant: passed=28, failed=0, skipped=0

## hotspot_review
- 触控行为: 普通模式单击打开、长按进入多选、多选模式单击仅切换选中。
- 删除行为: 仅删除文件项；删除按钮在选中数量 > 0 时启用并显示计数；删除前二次确认。
- 生命周期: 新增长按定时器在窗口关闭时停止并解绑 Tick 事件。

## rollback
1. `git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
2. `git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs`
3. `git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.State.cs`
4. `git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs`
5. `git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Lifecycle.cs`
