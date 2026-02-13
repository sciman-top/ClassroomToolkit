# ClassroomToolkit 架构改造验收清单（2026-02-13）

## 1. 启动与基础
- [ ] 应用可正常启动，无异常弹窗
- [ ] 主窗口位置与样式正常

## 2. 课堂主流程
- [ ] 画笔可正常打开/关闭
- [ ] 点名窗口可正常打开/关闭
- [ ] 照片教学可正常进入/退出
- [ ] 最小化与恢复行为正常

## 3. 多窗口编排
- [ ] Overlay/Toolbar/RollCall Owner 链正确
- [ ] Topmost 切换符合预期，无焦点抖动
- [ ] 退出时窗口可全部正常回收

## 4. 设置持久化
- [ ] 画笔设置修改后可保存并重启恢复
- [ ] 启动器设置修改后可保存并重启恢复
- [ ] 点名设置修改后可保存并重启恢复

## 5. DPI 场景（4K + 投影）
- [ ] 主窗口无明显模糊
- [ ] 画笔轨迹与鼠标位置一致
- [ ] 跨屏拖动后坐标与缩放正常

## 6. 自动化验证
- [x] `dotnet build ClassroomToolkit.sln -c Debug`
- [x] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- [x] `powershell -File scripts/ctoolkit.ps1 -SkipCommit`

## 7. 回滚预案
- [x] 每个任务有独立提交可回滚
- [ ] 出现异常时可按任务粒度快速回退

## 8. 本轮执行记录
- [x] 已配置 ApplicationManifest=app.manifest
- [x] app.manifest 已启用 PerMonitorV2

## 9. 收尾记录
- [x] dotnet build ClassroomToolkit.sln -c Release`r
- [x] dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Release`r
- [ ] 4K + 投影人工验收（需真实双屏环境）
- [ ] 课堂主流程人工验收（需教师端场景）

