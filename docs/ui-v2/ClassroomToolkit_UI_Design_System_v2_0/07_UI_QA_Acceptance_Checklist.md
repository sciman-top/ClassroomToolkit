# ClassroomToolkit UI 2.0 QA / Acceptance Checklist

## A. 全局一致性

- [ ] 所有窗口 TitleBar 高度统一
- [ ] 所有普通 Button 高度统一
- [ ] Primary/Secondary/Warning/Danger 语义一致
- [ ] 所有 Tab 视觉一致
- [ ] 所有 Slider 轨道/thumb 一致
- [ ] 所有 ComboBox 一致
- [ ] 所有 ContextMenu 一致
- [ ] 所有 Dialog Footer 顺序一致
- [ ] 字体族统一
- [ ] 普通正文无滥用 Bold
- [ ] 图标风格统一
- [ ] 无 Emoji 风格图标混入

## B. Token 合规

- [ ] Feature XAML 无无理由硬编码颜色
- [ ] Theme-dependent Brush 使用 DynamicResource
- [ ] Metrics 使用统一资源
- [ ] 没有 `TealBrush/GreenButton` 等视觉色名资源
- [ ] 三套主题切换不需要重启

## C. MidnightTeal

- [ ] 默认主题为 MidnightTeal
- [ ] 白色 PPT 上 Toolbar 边界清楚
- [ ] 黑色背景上 Toolbar 仍清楚
- [ ] Primary 不过亮、不荧光
- [ ] Warning 与 Danger 可明显区分
- [ ] 大面积背景不是纯黑

## D. Blackboard

- [ ] 不出现仿木框
- [ ] 不使用粉笔字体
- [ ] 不使用强纹理
- [ ] 文字对比度足够
- [ ] Warning 不与背景混淆
- [ ] 整体仍保持现代 Windows 软件感

## E. Light

- [ ] Window 与 Canvas 有边界
- [ ] Toolbar 在白色 PPT 上仍可辨认
- [ ] TextSecondary 不过浅
- [ ] Selection/Focus 明显
- [ ] Shadow 不过重

## F. 随机点名

- [ ] 姓名是第一视觉焦点
- [ ] 远距离可读
- [ ] 学号不抢姓名
- [ ] 开始/停止按钮明确
- [ ] 分组状态明显
- [ ] 名单/设置为次级
- [ ] 窗口缩放/DPI 不截断姓名

## G. 倒计时

- [ ] 10:00 等数字宽度稳定
- [ ] 数字远距离可读
- [ ] 环形装饰不抢焦点
- [ ] 开始/暂停/继续状态清楚
- [ ] Reset 为 Warning 语义
- [ ] 倒计时运行不因切主题中断

## H. 悬浮工具栏

- [ ] Height 约 52 DIP
- [ ] 工具点击区 ≥ 40
- [ ] Active 颜色 + 背景都变化
- [ ] 无持续 glow
- [ ] 组间分隔明确
- [ ] 低频工具可进入 More
- [ ] 工具条不因 DPI 过长溢出
- [ ] 在白/黑/绿背景都可辨认
- [ ] 不破坏点击穿透/Topmost/焦点
- [ ] 触控无明显误触

## I. 设置对话框

- [ ] 无 Card 套 Card 过度分组
- [ ] Section 间距清楚
- [ ] Reset 在左，Cancel/Confirm 在右
- [ ] Enter / Esc 行为正确
- [ ] Slider 数值不抖动
- [ ] Tab 高度一致
- [ ] 说明文字不抢主控件

## J. 文件管理

- [ ] 符合 Explorer 心智模型
- [ ] 左侧导航清楚
- [ ] 缩略图大小一致
- [ ] Selected/Focus/Hover 状态不同
- [ ] 长文件名处理正确
- [ ] Grid/List 一致
- [ ] 不过度使用品牌色

## K. 可访问性 / 输入

- [ ] Keyboard Focus 可见
- [ ] Tab 顺序合理
- [ ] Disabled 状态可理解
- [ ] 不依赖 Hover 才能操作
- [ ] 触控命中区域达标
- [ ] 200% DPI 无控件重叠
- [ ] Windows 10/11 均无明显异常

## L. 业务回归

- [ ] 点名正常
- [ ] 计时正常
- [ ] 图片/PDF 正常
- [ ] 白板正常
- [ ] 画笔撤销/重做正常
- [ ] 学生照片正常
- [ ] PPT/WPS 正常
- [ ] 多窗口层级正常
- [ ] 配置保存安全
- [ ] 主题切换不会修改学生数据
