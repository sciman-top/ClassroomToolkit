# BorderBrush DependencyProperty.UnsetValue 错误修复

## 问题描述
应用程序在显示诊断对话框时出现错误：
```
System.InvalidOperationException: "{DependencyProperty.UnsetValue}"不是属性"BorderBrush"的有效值。
```

## 错误根因分析

### 1. BorderBrush 属性未设置
WPF 中的 `Border` 控件如果设置了 `CornerRadius` 但没有明确设置 `BorderBrush`，在某些情况下会导致 `BorderBrush` 属性被设置为 `{DependencyProperty.UnsetValue}`，这会在布局计算时引发异常。

### 2. 样式继承问题
当 `Border` 控件从样式或模板中继承属性时，如果 `BorderBrush` 没有被正确设置，可能会出现无效值。

### 3. 布局计算时机
错误发生在 `Border.ArrangeOverride` 方法中，说明是在布局计算阶段检测到的问题。

## 修复方案

### 1. 修复 DiagnosticsDialog.xaml
**文件**: `src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml`
**问题**: 第24行的 Border 没有设置 BorderBrush
**修复**: 添加 `BorderBrush="#E0E0E0"`

```xml
<!-- 修复前 -->
<Border Background="White" CornerRadius="8" Margin="10">

<!-- 修复后 -->
<Border Background="White" BorderBrush="#E0E0E0" CornerRadius="8" Margin="10">
```

### 2. 修复 StudentListDialog.xaml
**文件**: `src/ClassroomToolkit.App/StudentListDialog.xaml`
**问题**: 第85行的 Border 没有设置 BorderBrush
**修复**: 添加 `BorderBrush="Transparent"`

```xml
<!-- 修复前 -->
<Border CornerRadius="16,16,0,0" Background="White"/>

<!-- 修复后 -->
<Border CornerRadius="16,16,0,0" Background="White" BorderBrush="Transparent"/>
```

### 3. 修复 RollCallWindow.xaml
**文件**: `src/ClassroomToolkit.App/RollCallWindow.xaml`
**问题**: 第151行的 Border 没有设置 BorderBrush
**修复**: 添加 `BorderBrush="Transparent"`

```xml
<!-- 修复前 -->
<Border CornerRadius="12" ClipToBounds="True">

<!-- 修复后 -->
<Border CornerRadius="12" ClipToBounds="True" BorderBrush="Transparent">
```

### 4. 修复 PhotoOverlayWindow.xaml
**文件**: `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
**问题**: 第40行的 Border 没有设置 BorderBrush
**修复**: 添加 `BorderBrush="Transparent"`

```xml
<!-- 修复前 -->
<Border x:Name="PhotoFrame" CornerRadius="20" Background="#111" ClipToBounds="True">

<!-- 修复后 -->
<Border x:Name="PhotoFrame" CornerRadius="20" Background="#111" ClipToBounds="True" BorderBrush="Transparent">
```

## 修复原则

### 1. 明确设置 BorderBrush
对于所有设置了 `CornerRadius` 的 `Border` 控件，都应该明确设置 `BorderBrush` 属性：
- 如果需要边框：设置具体的颜色值
- 如果不需要边框：设置 `BorderBrush="Transparent"`

### 2. 避免依赖默认值
不要依赖 WPF 的默认属性值，特别是对于设置了 `CornerRadius` 的 `Border` 控件。

### 3. 一致性检查
所有 XAML 文件中的 `Border` 控件都应该进行一致性检查，确保没有遗漏的 `BorderBrush` 设置。

## 预期效果
- ✅ 消除 `DependencyProperty.UnsetValue` 错误
- ✅ 诊断对话框能够正常显示
- ✅ 所有窗口的布局计算正常
- ✅ 保持原有的视觉效果

## 技术细节
- **错误类型**: `System.InvalidOperationException`
- **错误位置**: `Border.ArrangeOverride` 方法
- **根本原因**: `BorderBrush` 属性未正确设置
- **修复策略**: 为所有相关 Border 控件添加明确的 BorderBrush 属性

## 验证方法
1. 重新构建应用程序
2. 打开诊断对话框（点击诊断按钮）
3. 确认没有异常抛出
4. 检查所有窗口的显示是否正常

这次修复应该能够彻底解决 BorderBrush 相关的异常问题。
