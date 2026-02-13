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
- [ ] `dotnet build ClassroomToolkit.sln -c Debug`
- [ ] `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- [ ] `powershell -File scripts/ctoolkit.ps1`

## 7. 回滚预案
- [ ] 每个任务有独立提交可回滚
- [ ] 出现异常时可按任务粒度快速回退
