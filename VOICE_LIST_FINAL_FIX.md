# 语音列表显示问题最终修复

## 问题现状
用户反馈语音列表又变回只有3个，而不是预期的6个。需要进一步诊断问题。

## 可能的原因分析

### 1. 语音名称不匹配
系统语音设置中显示的语音名称可能与应用程序中检测到的名称不同：
- 系统设置：Microsoft Huihui, Microsoft Yaoyao, Microsoft Kangkang
- 应用检测：Microsoft Huihui Desktop, Microsoft Yaoyao Desktop, Microsoft Kangkang Desktop

### 2. 语音启用状态
某些语音可能被标记为未启用（`voice.Enabled = false`），导致被过滤。

### 3. 语言分组问题
语言分组逻辑可能有问题，某些语音可能被错误分类。

### 4. 系统语音版本差异
不同版本的 Windows 可能有不同的语音包和命名规则。

## 当前修复措施

### 1. 完全禁用过滤
```csharp
private static readonly HashSet<string> SilentVoices = new(StringComparer.OrdinalIgnoreCase)
{
    // 完全清空过滤列表，确保显示所有语音
    // 如果需要过滤，请基于实际的语音名称进行过滤
};
```

### 2. 详细调试信息
```csharp
// 详细调试信息
System.Diagnostics.Debug.WriteLine($"检测语音: {info.Name} (启用: {voice.Enabled}, 文化: {info.Culture.Name})");

// 根据语言分组
if (info.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
{
    chineseVoices.Add(voice);
    System.Diagnostics.Debug.WriteLine($"添加到中文语音: {info.Name}");
}
else
{
    otherVoices.Add(voice);
    System.Diagnostics.Debug.WriteLine($"添加到其他语音: {info.Name}");
}
```

### 3. 移除排序过滤
```csharp
// 添加中文发音人
foreach (var voice in chineseVoices)
{
    var info = voice.VoiceInfo;
    var label = FormatVoiceLabel(info, true, voice.Enabled);
    voices.Add(new ComboOption(info.Name, label));
    System.Diagnostics.Debug.WriteLine($"最终添加中文语音: {info.Name}");
}
```

## 诊断步骤

### 1. 查看调试输出
重新打开点名设置窗口，查看 Visual Studio 输出窗口或 DebugView，找到以下信息：
- "检测语音: ..." - 显示所有检测到的语音
- "添加到中文语音: ..." - 显示分类到中文的语音
- "添加到其他语音: ..." - 显示分类到其他的语音
- "最终添加..." - 显示最终添加到下拉框的语音

### 2. 运行独立测试
可以编译运行 `voice_test.cs` 来查看系统中实际的语音列表。

### 3. 检查语音名称对比
对比系统语音设置中的名称与应用检测到的名称，看是否有差异。

## 可能的进一步修复

### 1. 语音名称映射
如果发现名称差异，可以添加名称映射逻辑：
```csharp
private static readonly Dictionary<string, string> VoiceNameMapping = new()
{
    { "Microsoft Huihui Desktop", "Microsoft Huihui" },
    { "Microsoft Yaoyao Desktop", "Microsoft Yaoyao" },
    { "Microsoft Kangkang Desktop", "Microsoft Kangkang" }
};
```

### 2. 强制显示所有语音
如果分类逻辑有问题，可以暂时显示所有语音：
```csharp
foreach (var voice in allVoices)
{
    var info = voice.VoiceInfo;
    var label = $"{info.Name} ({info.Culture.Name}, {info.Gender})";
    voices.Add(new ComboOption(info.Name, label));
}
```

### 3. 检查语音引擎兼容性
确认当前选择的语音引擎是否支持所有检测到的语音。

## 预期结果
修复后应该显示系统中所有可用的语音，包括：
- 中文语音：Huihui、Yaoyao、Kangkang
- 英文语音：David、Zira、Mark

## 验证方法
1. 重新打开点名设置窗口
2. 查看发音人下拉框中的选项数量
3. 检查调试输出中的语音检测信息
4. 对比系统语音设置与应用显示的差异

## 技术细节
- **关键文件**：`src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
- **关键方法**：`BuildSapiVoices()`, `BuildVoiceCombo()`
- **调试工具**：Visual Studio 输出窗口、DebugView
- **测试文件**：`voice_test.cs`

这次修复应该能够显示所有系统语音，如果还有问题，调试信息会告诉我们具体原因。
