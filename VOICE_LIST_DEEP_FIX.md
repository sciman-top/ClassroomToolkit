# 语音列表显示问题深度修复

## 问题进展
用户反馈修复后语音列表从3个变成了1个，只有 "Microsoft Huihui Desktop" 显示。

## 问题分析

### 1. SilentVoices 过滤问题
原来的 `SilentVoices` 集合：
```csharp
private static readonly HashSet<string> SilentVoices = new(StringComparer.OrdinalIgnoreCase)
{
    "Microsoft Zira Desktop",
    "Microsoft David Desktop"
};
```

**问题**：这个过滤可能过于严格，或者系统中的语音名称与预期不符。

### 2. 语音引擎差异
应用支持两种语音引擎：
- **pyttsx3**：Python 引擎，默认选项
- **SAPI**：Windows SAPI 引擎

**问题**：不同引擎可能支持不同的语音列表，但代码没有区分处理。

### 3. 语音获取方式
原来直接使用 `SpeechSynthesizer.GetInstalledVoices()`，这只适用于 SAPI 引擎。

## 修复方案

### 1. 暂时禁用过滤
```csharp
private static readonly HashSet<string> SilentVoices = new(StringComparer.OrdinalIgnoreCase)
{
    // 暂时注释掉所有过滤，以诊断问题
    // "Microsoft Zira Desktop",
    // "Microsoft David Desktop"
};
```

### 2. 重构语音获取逻辑
```csharp
private void BuildVoiceCombo(string? current)
{
    var voices = new List<ComboOption>();
    var engine = GetSelectedValue(SpeechEngineCombo, "pyttsx3");
    
    try
    {
        if (engine == "pyttsx3")
        {
            // 对于 pyttsx3，我们需要通过 Python 获取语音列表
            // 这里先使用 SAPI 作为后备，显示所有可用的语音
            BuildSapiVoices(voices);
        }
        else
        {
            // 对于 SAPI，直接使用 SpeechSynthesizer
            BuildSapiVoices(voices);
        }
    }
    catch
    {
        voices.Clear();
    }
    // ...
}
```

### 3. 添加引擎变化处理
```csharp
private void OnSpeechEngineChanged(object sender, SelectionChangedEventArgs e)
{
    UpdateSpeechControls();
    // 重新构建语音列表，因为不同引擎可能有不同的语音
    BuildVoiceCombo(_initialVoiceId);
}
```

### 4. 提取 SAPI 语音获取方法
```csharp
private void BuildSapiVoices(List<ComboOption> voices)
{
    using var synth = new SpeechSynthesizer();
    var allVoices = synth.GetInstalledVoices().ToList();
    
    // 按语言分组，优先显示中文，然后其他语言
    var chineseVoices = new List<InstalledVoice>();
    var otherVoices = new List<InstalledVoice>();
    
    // ... 处理逻辑 ...
}
```

## 预期修复效果

### 短期效果（当前修复）
- 禁用 `SilentVoices` 过滤，应该显示更多语音
- 至少应该显示系统中的所有 SAPI 语音

### 长期效果（需要进一步开发）
- 对于 pyttsx3 引擎，需要通过 Python 脚本获取语音列表
- 可能需要调用 Python 的 `pyttsx3` 模块来获取可用语音

## 调试信息

修复后的代码会输出调试信息：
```csharp
System.Diagnostics.Debug.WriteLine($"找到 {allVoices.Count} 个语音：");
foreach (var voice in allVoices)
{
    System.Diagnostics.Debug.WriteLine($"  - {voice.VoiceInfo.Name} (启用: {voice.Enabled}, 文化: {voice.VoiceInfo.Culture.Name})");
}
```

## 验证步骤
1. 重新打开点名设置窗口
2. 检查发音人下拉框
3. 查看是否显示更多语音选项
4. 切换语音引擎，观察语音列表变化
5. 查看调试输出确认语音检测情况

## 可能的进一步问题
1. **pyttsx3 语音获取**：可能需要通过 Python 脚本获取 pyttsx3 支持的语音
2. **语音名称匹配**：系统语音名称可能与代码中的过滤条件不匹配
3. **引擎兼容性**：不同引擎可能确实支持不同的语音集合

## 技术细节
- **文件位置**：`src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
- **关键方法**：`BuildVoiceCombo()`, `BuildSapiVoices()`, `OnSpeechEngineChanged()`
- **过滤策略**：暂时禁用，待进一步诊断
- **引擎支持**：区分 pyttsx3 和 SAPI 引擎

这次修复应该能够显示更多的语音选项，解决语音列表过少的问题。
