规则ID=R1,R2,R3,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs; src/ClassroomToolkit.App/Photos/ImageManagerWindow.IO.cs
当前落点=ImageManagerWindow 缩略图模式打开大目录时存在明显卡顿
目标归宿=降低缩略图生成瞬时负载并复用目录内已生成缩略图缓存
迁移批次=20260401-imagemanager-thumbnail-open-lag
风险等级=低
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet build ClassroomToolkit.sln -c Debug -p:UseAppHost=false; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=UseAppHost=false build 通过（0 error）；全量 test 3053/3053 通过；contract/invariant 24/24 通过；hotspot PASS
回滚动作=git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs src/ClassroomToolkit.App/Photos/ImageManagerWindow.IO.cs
gate_na=reason: 常规 build 被运行中的 ClassroomToolkit.exe 锁定（MSB3027/MSB3021） | alternative_verification: 使用 dotnet build -p:UseAppHost=false 完成同源码编译校验，随后执行 --no-build 的 test/contract 与 hotspot | evidence_link: docs/change-evidence/20260401-imagemanager-thumbnail-open-lag.md | expires_at: 2026-04-08

补充变更=缩略图工作线程并发上限从按 CPU 动态 2-4 调整为 1-2；缩略图解码宽度改为随当前缩略图尺寸自适应（96-320）；新增 512 容量 LRU 内存缓存（按 路径+类型+解码宽度+修改时间 校验）并在窗口关闭时清理。
补充预期=首次打开大目录时 UI 卡顿降低；再次打开同目录时缩略图可直接命中缓存，滚动与进入目录更顺滑。
