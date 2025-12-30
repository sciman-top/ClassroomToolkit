using System;
using System.Speech.Synthesis;
using System.Linq;

class Program
{
    static void Main()
    {
        Console.WriteLine("系统中安装的所有语音：");
        Console.WriteLine("========================");
        
        try
        {
            using var synth = new SpeechSynthesizer();
            var allVoices = synth.GetInstalledVoices().ToList();
            
            Console.WriteLine($"总共找到 {allVoices.Count} 个语音：");
            Console.WriteLine();
            
            foreach (var voice in allVoices)
            {
                var info = voice.VoiceInfo;
                Console.WriteLine($"名称: {info.Name}");
                Console.WriteLine($"文化: {info.Culture.Name} ({info.Culture.DisplayName})");
                Console.WriteLine($"性别: {info.Gender}");
                Console.WriteLine($"年龄: {info.Age}");
                Console.WriteLine($"启用: {voice.Enabled}");
                Console.WriteLine($"描述: {info.Description}");
                Console.WriteLine($"ID: {info.Id}");
                Console.WriteLine("---");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
    }
}
