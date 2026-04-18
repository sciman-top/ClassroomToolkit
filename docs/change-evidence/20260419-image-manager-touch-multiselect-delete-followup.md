# 20260419-image-manager-touch-multiselect-delete-followup

- rule_id: R2,R6,R8
- risk_level: medium
- topic: 资源管理按钮裁剪与取消多选崩溃修复
- scope:
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml
  - src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs

## fixes
1. 将“添加收藏/取消收藏/清空最近”容器从 `UniformGrid` 改为三列等宽 `Grid`，移除 `MinWidth` 造成的裁剪风险。
2. 修复退出多选顺序：先 `SelectedItems.Clear()` 再切回 `SelectionMode.Single`，避免 `InvalidOperationException`。
3. 多选模式下在 `PointerDown` 拦截默认选中，避免与自定义 `PointerUp` 切换叠加导致反选。

## gate_results
- build (required): `dotnet build ClassroomToolkit.sln -c Debug`
  - gate_na: true
  - reason: `sciman Classroom Toolkit.exe` 正在运行并锁定 `bin/Debug` 输出。
  - alternative_verification: `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug -p:OutDir=.../artifacts/tmpbuild/` 通过。
- test (required): `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - gate_na: true
  - reason: 同上，原路径输出受运行进程占用会阻断稳定执行。
  - alternative_verification: `dotnet test ... -p:OutDir=.../artifacts/tmpbuild-tests-full/` 执行，3272 passed / 1 failed（缺少基线文件 `D:\OneDrive\Baselines\brush-dpi-golden.json`）。
- contract/invariant (required): 通过
  - command: `dotnet test ... --filter "ArchitectureDependencyTests|InteropHookLifecycleContractTests|InteropHookEventDispatchContractTests|GlobalHookServiceLifecycleContractTests|CrossPageDisplayLifecycleContractTests" -p:OutDir=.../artifacts/tmpbuild-tests-contract/`
  - result: 28 passed / 0 failed
- hotspot: 已复核通过

## rollback
1. `git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
2. `git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs`
