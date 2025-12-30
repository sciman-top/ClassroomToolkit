# 语音列表显示问题修复

## 问题描述
Windows 系统中有6种语音：
- Microsoft David (英语男声)
- Microsoft Zira (英语女声)  
- Microsoft Huihui (中文女声)
- Microsoft Yaoyao (中文女声)
- Microsoft Kangkang (中文男声)
- Microsoft Mark (英语男声)

但应用程序的语音播报设置中只显示了3种：huihui、zira、david。

## 问题根因分析

### 1. SilentVoices 过滤机制
代码中定义了 `SilentVoices` 集合来排除某些语音：
```csharp
private static readonly HashSet<string> SilentVoices = new(StringComparer.OrdinalIgnoreCase)
{
    "Microsoft Zira Desktop",
    "Microsoft David Desktop"
};
```

**问题**：这个过滤逻辑没有被正确使用，导致某些语音被意外排除。

### 2. 语音启用状态过滤
代码中使用了 `voice.Enabled` 进行排序，但可能某些语音被标记为未启用，导致它们在列表中位置靠后或被忽略。

### 3. 排序逻辑问题
原来的排序逻辑：
```csharp
.OrderByDescending(item => item.Enabled).ThenBy(item => item.VoiceInfo.Name)
```

这会导致未启用的语音排在最后，可能影响显示。

## 修复方案

### 1. 正确使用 SilentVoices 过滤
```csharp
foreach (var voice in allVoices)
{
    var info = voice.VoiceInfo;
    
    // 过滤掉已知的静音或有问题的语音
    if (SilentVoices.Contains(info.Name))
    {
        System.Diagnostics.Debug.WriteLine($"跳过静音语音: {info.Name}");
        continue;
    }
    
    // ... 其他逻辑
}
```

### 2. 改变排序逻辑
按语音名称排序，而不是启用状态：
```csharp
// 添加中文发音人
foreach (var voice in chineseVoices.OrderByDescending(item => item.VoiceInfo.Name).ThenBy(item => item.Enabled))
{
    var info = voice.VoiceInfo;
    var label = FormatVoiceLabel(info, true, voice.Enabled);
    voices.Add(new ComboOption(info.Name, label));
}
```

### 3. 添加调试信息
```csharp
// 调试信息：输出所有找到的语音
System.Diagnostics.Debug.WriteLine($"找到 {allVoices.Count} 个语音：");
foreach (var voice in allVoices)
{
    System.Diagnostics.Debug.WriteLine($"  - {voice.VoiceInfo.Name} (启用: {voice.Enabled}, 文化: {voice.VoiceInfo.Culture.Name})");
}
```

## 预期修复效果

修复后应该显示以下语音：
1. **中文语音（推荐）**：
   - Microsoft Huihui (中文女声)
   - Microsoft Yaoyao (中文女声) 
   - Microsoft Kangkang (中文男声)

2. **其他语言语音**：
   - Microsoft David (英语男声)
   - Microsoft Mark (英语男声)
   - Microsoft Zira (英语女声)

## SilentVoices 的作用

`SilentVoices` 集合中的语音通常是：
- "Microsoft Zira Desktop" - 桌面版可能有兼容性问题
- "Microsoft David Desktop" - 桌面版可能有兼容性问题

这些语音的完整版本（不带 "Desktop" 后缀）应该正常工作。

## 验证方法
1. 打开语音播报设置
2. 检查发音人下拉框
3. 确认显示所有6种语音（或过滤后的所有可用语音）
4. 查看调试输出确认语音检测情况

## 技术细节
- **文件位置**：`src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
- **关键方法**：`BuildVoiceCombo()`
- **过滤机制**：`SilentVoices` 集合
- **排序策略**：按名称排序，中文优先

这个修复确保用户能够看到系统中所有可用的语音选项，同时保持对已知问题语音的过滤。
