using System;
using System.Speech.Synthesis;
using System.Linq;

class VoiceTest
{
    static void Main()
    {
        Console.WriteLine("=== 系统语音检测测试 ===");
        
        try
        {
            using var synth = new SpeechSynthesizer();
            var allVoices = synth.GetInstalledVoices().ToList();
            
            Console.WriteLine($"总共找到 {allVoices.Count} 个语音：");
            Console.WriteLine();
            
            int index = 1;
            foreach (var voice in allVoices)
            {
                var info = voice.VoiceInfo;
                Console.WriteLine($"{index++}. {info.Name}");
                Console.WriteLine($"   文化: {info.Culture.Name} ({info.Culture.DisplayName})");
                Console.WriteLine($"   性别: {info.Gender}");
                Console.WriteLine($"   年龄: {info.Age}");
                Console.WriteLine($"   启用: {voice.Enabled}");
                Console.WriteLine($"   描述: {info.Description}");
                Console.WriteLine($"   ID: {info.Id}");
                Console.WriteLine();
            }
            
            // 测试语音分组
            var chineseVoices = allVoices.Where(v => v.VoiceInfo.Culture.Name.StartsWith("zh")).ToList();
            var otherVoices = allVoices.Where(v => !v.VoiceInfo.Culture.Name.StartsWith("zh")).ToList();
            
            Console.WriteLine($"中文语音: {chineseVoices.Count} 个");
            foreach (var voice in chineseVoices)
            {
                Console.WriteLine($"  - {voice.VoiceInfo.Name}");
            }
            
            Console.WriteLine($"其他语音: {otherVoices.Count} 个");
            foreach (var voice in otherVoices)
            {
                Console.WriteLine($"  - {voice.VoiceInfo.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine($"详细信息: {ex}");
        }
        
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
}
