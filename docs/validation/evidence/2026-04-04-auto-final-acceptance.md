# Final Acceptance Evidence

- generated_at_utc: 2026-04-03T18:31:10.8371315+00:00
- configuration: Debug
- solution: ClassroomToolkit.sln
- test_project: tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj
- output_path: docs\validation\evidence\2026-04-04-auto-final-acceptance.md

## Gate Result
- status: passed
- gate_order: build -> test -> contract/invariant -> hotspot -> release_test

## Manual Gate
- manual_final_regression: gate_na
- reason: User explicitly requested skipping manual final regression gate.
- alternative_verification: Use automated gate sequence build->test->contract/invariant->hotspot->release_test as temporary alternative evidence.
- evidence_link: docs\validation\evidence\2026-04-04-auto-final-acceptance.md
- expires_at: 2026-05-04
- recovery_plan: Run docs/validation/manual-final-regression-checklist.md and update acceptance docs before next release.

## Commands
### Step 1: build
- cmd: dotnet build ClassroomToolkit.sln -c Debug
- exit_code: 0
- started_utc: 2026-04-03T18:30:46.2630890+00:00
- finished_utc: 2026-04-03T18:30:48.2473043+00:00
- duration_ms: 1984
- key_output:
```text
正在确定要还原的项目…
  所有项目均是最新的，无法还原。
  ClassroomToolkit.Domain -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Domain\bin\Debug\net10.0\ClassroomToolkit.Domain.dll
  ClassroomToolkit.Interop -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Interop\bin\Debug\net10.0-windows\ClassroomToolkit.Interop.dll
  ClassroomToolkit.Application -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Application\bin\Debug\net10.0\ClassroomToolkit.Application.dll
  ClassroomToolkit.Infra -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Infra\bin\Debug\net10.0\ClassroomToolkit.Infra.dll
  ClassroomToolkit.Services -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Services\bin\Debug\net10.0-windows\ClassroomToolkit.Services.dll
  ClassroomToolkit.App -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\bin\Debug\net10.0-windows\sciman Classroom Toolkit.dll
  ClassroomToolkit.Tests -> E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin\Debug\net10.0-windows\ClassroomToolkit.Tests.dll

已成功生成。
    0 个警告
    0 个错误

已用时间 00:00:01.70
```

### Step 2: test
- cmd: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug
- exit_code: 0
- started_utc: 2026-04-03T18:30:48.2565888+00:00
- finished_utc: 2026-04-03T18:30:56.2199921+00:00
- duration_ms: 7963
- key_output:
```text
正在确定要还原的项目…
  所有项目均是最新的，无法还原。
  ClassroomToolkit.Domain -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Domain\bin\Debug\net10.0\ClassroomToolkit.Domain.dll
  ClassroomToolkit.Interop -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Interop\bin\Debug\net10.0-windows\ClassroomToolkit.Interop.dll
  ClassroomToolkit.Application -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Application\bin\Debug\net10.0\ClassroomToolkit.Application.dll
  ClassroomToolkit.Services -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Services\bin\Debug\net10.0-windows\ClassroomToolkit.Services.dll
  ClassroomToolkit.Infra -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Infra\bin\Debug\net10.0\ClassroomToolkit.Infra.dll
  ClassroomToolkit.App -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\bin\Debug\net10.0-windows\sciman Classroom Toolkit.dll
  ClassroomToolkit.Tests -> E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin\Debug\net10.0-windows\ClassroomToolkit.Tests.dll
E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin\Debug\net10.0-windows\ClassroomToolkit.Tests.dll (.NETCoreApp,Version=v10.0)的测试运行
总共 1 个测试文件与指定模式相匹配。

已通过! - 失败:     0，通过:  3171，已跳过:     0，总计:  3171，持续时间: 2 s - ClassroomToolkit.Tests.dll (net10.0)
```

### Step 3: contract_invariant
- cmd: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests
- exit_code: 0
- started_utc: 2026-04-03T18:30:56.2199921+00:00
- finished_utc: 2026-04-03T18:31:01.7997550+00:00
- duration_ms: 5580
- key_output:
```text
正在确定要还原的项目…
  所有项目均是最新的，无法还原。
  ClassroomToolkit.Interop -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Interop\bin\Debug\net10.0-windows\ClassroomToolkit.Interop.dll
  ClassroomToolkit.Domain -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Domain\bin\Debug\net10.0\ClassroomToolkit.Domain.dll
  ClassroomToolkit.Application -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Application\bin\Debug\net10.0\ClassroomToolkit.Application.dll
  ClassroomToolkit.Services -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Services\bin\Debug\net10.0-windows\ClassroomToolkit.Services.dll
  ClassroomToolkit.Infra -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Infra\bin\Debug\net10.0\ClassroomToolkit.Infra.dll
  ClassroomToolkit.App -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\bin\Debug\net10.0-windows\sciman Classroom Toolkit.dll
  ClassroomToolkit.Tests -> E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin\Debug\net10.0-windows\ClassroomToolkit.Tests.dll
E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin\Debug\net10.0-windows\ClassroomToolkit.Tests.dll (.NETCoreApp,Version=v10.0)的测试运行
总共 1 个测试文件与指定模式相匹配。

已通过! - 失败:     0，通过:    25，已跳过:     0，总计:    25，持续时间: 250 ms - ClassroomToolkit.Tests.dll (net10.0)
```

### Step 4: hotspot
- cmd: powershell -File scripts/quality/check-hotspot-line-budgets.ps1
- exit_code: 0
- started_utc: 2026-04-03T18:31:01.8017602+00:00
- finished_utc: 2026-04-03T18:31:02.3859950+00:00
- duration_ms: 584
- key_output:
```text
[hotspot] budgetFile=scripts/quality/hotspot-line-budgets.json entries=15

path                                                                          maxLines actualLines delta   ok
----                                                                          -------- ----------- -----   --
src/ClassroomToolkit.App/Ink/InkExportService.cs                                  1240        1111  -129 True
src/ClassroomToolkit.App/MainWindow.xaml.cs                                        900         822   -78 True
src/ClassroomToolkit.App/Paint/Brushes/VariableWidthBrushRenderer.Geometry.cs     1450        1217  -233 True
src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs                          1420        1419    -1 True
src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs                        1650        1490  -160 True
src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs              2380        1929  -451 True
src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs             1340        1256   -84 True
src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Transform.cs              1100        1005   -95 True
src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs                         1450        1380   -70 True
src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml.cs                        1880        1748  -132 True
src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs                        1540        1390  -150 True
src/ClassroomToolkit.App/Windowing/OverlayNavigationFocusPolicy.cs                 220         182   -38 True
src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs                          380         319   -61 True
src/ClassroomToolkit.Interop/Presentation/Win32PresentationResolver.cs             370         313   -57 True
src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs            370         314   -56 True


[hotspot] status=PASS
```

### Step 5: release_test
- cmd: dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release
- exit_code: 0
- started_utc: 2026-04-03T18:31:02.3859950+00:00
- finished_utc: 2026-04-03T18:31:10.7883146+00:00
- duration_ms: 8402
- key_output:
```text
正在确定要还原的项目…
  所有项目均是最新的，无法还原。
  ClassroomToolkit.Domain -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Domain\bin\Release\net10.0\ClassroomToolkit.Domain.dll
  ClassroomToolkit.Interop -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Interop\bin\Release\net10.0-windows\ClassroomToolkit.Interop.dll
  ClassroomToolkit.Application -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Application\bin\Release\net10.0\ClassroomToolkit.Application.dll
  ClassroomToolkit.Services -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Services\bin\Release\net10.0-windows\ClassroomToolkit.Services.dll
  ClassroomToolkit.Infra -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.Infra\bin\Release\net10.0\ClassroomToolkit.Infra.dll
  ClassroomToolkit.App -> E:\CODE\ClassroomToolkit\src\ClassroomToolkit.App\bin\Release\net10.0-windows\sciman Classroom Toolkit.dll
  ClassroomToolkit.Tests -> E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin\Release\net10.0-windows\ClassroomToolkit.Tests.dll
E:\CODE\ClassroomToolkit\tests\ClassroomToolkit.Tests\bin\Release\net10.0-windows\ClassroomToolkit.Tests.dll (.NETCoreApp,Version=v10.0)的测试运行
总共 1 个测试文件与指定模式相匹配。

已通过! - 失败:     0，通过:  3171，已跳过:     0，总计:  3171，持续时间: 2 s - ClassroomToolkit.Tests.dll (net10.0)
```

