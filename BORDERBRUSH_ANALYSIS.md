# BorderBrush 代码分析规则

## 问题模式
```xml
<!-- ❌ 错误模式 -->
<Border CornerRadius="8" Background="White">
<Border CornerRadius="8" Background="White" Margin="10">
<Border CornerRadius="8" Background="{StaticResource Brush_Background}">
```

## 正确模式
```xml
<!-- ✅ 正确模式 -->
<Border CornerRadius="8" Background="White" BorderBrush="Transparent">
<Border CornerRadius="8" Background="White" Margin="10" BorderBrush="Transparent">
<Border CornerRadius="8" Background="{StaticResource Brush_Background}" BorderBrush="Transparent">
```

## 自动修复脚本
```powershell
# PowerShell 脚本自动修复 BorderBrush 问题
Get-ChildItem -Path "." -Filter "*.xaml" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    
    # 查找有 CornerRadius 但没有 BorderBrush 的 Border
    $pattern = '(<Border[^>]*CornerRadius[^>]*>)(?!.*BorderBrush)'
    $replacement = '$1 BorderBrush="Transparent"'
    
    $content = $content -replace $pattern, $replacement
    
    Set-Content $_.FullName $content
    Write-Host "Fixed: $($_.Name)"
}
```

## 预防措施
1. **代码审查清单**: 每次添加 Border 控件时检查 BorderBrush
2. **模板规范**: 所有 ControlTemplate 中的 Border 必须设置 BorderBrush
3. **自动化检查**: 在构建时运行 XAML 验证工具
4. **团队培训**: 向团队成员普及这个问题

## 为什么 WPF 有这个问题
1. **历史原因**: WPF 的早期版本对依赖属性验证不够严格
2. **设计缺陷**: CornerRadius 和 BorderBrush 的依赖关系设计不合理
3. **向后兼容**: 微软不能轻易修复这个问题，因为会破坏现有代码

## 最佳实践
1. **总是设置 BorderBrush**: 即使是透明边框也要明确设置
2. **使用 SafeBorder**: 对于复杂的模板，使用自定义的安全控件
3. **自动化验证**: 在 CI/CD 中集成 XAML 验证
4. **代码生成**: 使用代码生成工具创建标准的 Border 控件
