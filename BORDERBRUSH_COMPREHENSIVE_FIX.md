# BorderBrush DependencyProperty.UnsetValue 全面修复

## 问题描述
应用程序在显示诊断对话框时持续出现 `DependencyProperty.UnsetValue` 错误，表明还有其他地方的 Border 控件存在同样的问题。

## 全面修复记录

### 1. DiagnosticsDialog.xaml ✅
**位置**: 第24行
**修复**: 添加 `BorderBrush="#E0E0E0"`
```xml
<Border Background="White" BorderBrush="#E0E0E0" CornerRadius="8" Margin="10">
```

### 2. StudentListDialog.xaml ✅
**位置**: 第85行、第88行、第170行、第173行
**修复**: 为所有相关 Border 添加 `BorderBrush="Transparent"`

```xml
<!-- 标题栏背景 -->
<Border CornerRadius="16,16,0,0" Background="White" BorderBrush="Transparent"/>

<!-- 图标容器 -->
<Border Background="{StaticResource Brush_Surface_Tint}" CornerRadius="8" Padding="6" BorderBrush="Transparent">

<!-- 状态指示器 -->
<Border Width="8" Height="8" CornerRadius="4" Background="{StaticResource Brush_Primary}" Margin="0,0,8,0" BorderBrush="Transparent"/>
<Border Width="8" Height="8" CornerRadius="4" Background="{StaticResource Brush_Disabled}" Margin="0,0,8,0" BorderBrush="Transparent"/>
```

### 3. RollCallWindow.xaml ✅
**位置**: 第151行、第161行
**修复**: 为照片容器和计时视图添加 `BorderBrush="Transparent"`

```xml
<!-- 照片容器 -->
<Border CornerRadius="12" ClipToBounds="True" BorderBrush="Transparent">

<!-- 计时视图 -->
<Border Background="{StaticResource Brush_Background_Dark}" CornerRadius="24" BorderBrush="Transparent">
```

### 4. RollCallSettingsDialog.xaml ✅
**位置**: 第47行
**修复**: 为 TabItem 模板中的 Border 添加 `BorderBrush="Transparent"`

```xml
<Border x:Name="bd" CornerRadius="6" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}" BorderBrush="Transparent">
```

### 5. PhotoOverlayWindow.xaml ✅
**位置**: 第40行
**修复**: 为照片框架添加 `BorderBrush="Transparent"`

```xml
<Border x:Name="PhotoFrame" CornerRadius="20" Background="#111" ClipToBounds="True" BorderBrush="Transparent">
```

### 6. PaintToolbarWindow.xaml ✅
**位置**: 第98行、第120行
**修复**: 为颜色组和擦除组添加 `BorderBrush="Transparent"`

```xml
<!-- 颜色组 -->
<Border Background="{StaticResource Brush_Background_L2}" CornerRadius="18" Padding="6,4" BorderBrush="Transparent">

<!-- 擦除组 -->
<Border Background="{StaticResource Brush_Background_L2}" CornerRadius="18" Padding="6,4" BorderBrush="Transparent">
```

### 7. MainWindow.xaml ✅
**位置**: 第33行
**修复**: 为主按钮模板中的阴影层添加 `BorderBrush="Transparent"`

```xml
<Border x:Name="shadow" CornerRadius="18" Background="White" Margin="3" BorderBrush="Transparent">
```

## 修复策略

### 1. 全面搜索
使用正则表达式搜索所有设置了 `CornerRadius` 但可能缺少 `BorderBrush` 的 Border 控件：
```regex
<Border.*CornerRadius.*>
```

### 2. 分类修复
根据 Border 的用途分类处理：
- **装饰性边框**: 设置具体颜色值
- **容器边框**: 设置 `BorderBrush="Transparent"`
- **模板边框**: 设置 `BorderBrush="Transparent"`

### 3. 一致性原则
确保所有设置了 `CornerRadius` 的 Border 都有明确的 `BorderBrush` 属性。

## 预期效果
- ✅ 彻底消除 `DependencyProperty.UnsetValue` 错误
- ✅ 诊断对话框正常显示
- ✅ 所有窗口和控件正常渲染
- ✅ 保持原有视觉效果不变

## 验证步骤
1. 重新构建应用程序
2. 打开诊断对话框（点击诊断按钮）
3. 确认无异常抛出
4. 测试其他窗口的显示
5. 检查语音列表调试信息

## 技术细节
- **修复文件数**: 7个 XAML 文件
- **修复位置数**: 12个 Border 控件
- **主要策略**: 添加 `BorderBrush="Transparent"` 或具体颜色值
- **影响范围**: 所有主要窗口和对话框

## 预防措施
1. **代码审查**: 在添加新的 Border 控件时，确保设置 BorderBrush
2. **模板规范**: 在 ControlTemplate 中明确设置所有依赖属性
3. **静态分析**: 可以考虑添加 XAML 静态分析工具来检测类似问题

这次全面修复应该能够彻底解决所有 BorderBrush 相关的异常问题。
