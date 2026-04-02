规则ID=R1,R2,R4,R6,R8
影响模块=src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml; src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs; src/ClassroomToolkit.App/Photos/ImageManagerWindow.ThumbnailScheduling.cs
当前落点=缩略图模式目录打开时仍存在“全量排队解码”导致首屏可感卡顿
目标归宿=缩略图加载改为“可见区域优先 + 后台补齐”
迁移批次=20260401-imagemanager-visible-priority-thumbnails
风险等级=低
执行命令=dotnet build ClassroomToolkit.sln -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"; powershell -File scripts/quality/check-hotspot-line-budgets.ps1
验证证据=build 0 error；全量 test 3053/3053 通过；contract/invariant 24/24 通过；hotspot PASS（ImageManagerWindow.xaml.cs 回到预算内）
回滚动作=git checkout -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs src/ClassroomToolkit.App/Photos/ImageManagerWindow.ThumbnailScheduling.cs

补充变更=为缩略图 ListView 增加滚动事件；新增分部文件承载缩略图调度逻辑；目录加载时先入待处理队列，再按可见区域优先派发，剩余任务由后台定时器分批补齐。
补充说明=保留此前目录级缩略图缓存与自适应解码能力，和本次“可见优先”策略叠加生效。
