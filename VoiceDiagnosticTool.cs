using System;
using System.Speech.Synthesis;
using System.IO;

/// <summary>
/// 语音诊断工具 - 检测系统中所有已安装的语音
/// 运行此工具将输出所有语音的详细信息到控制台和文本文件
/// </summary>
class VoiceDiagnosticTool
{
    static void Main()
    {
        Console.WriteLine("=== Windows 语音诊断工具 ===");
        Console.WriteLine($"检测时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        try
        {
            using var synth = new SpeechSynthesizer();
            var allVoices = synth.GetInstalledVoices();

            Console.WriteLine($"系统中共有 {allVoices.Count} 个语音包\n");

            var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "voice_diagnostic.txt");
            using var writer = new StreamWriter(logFile, append: false);
            writer.WriteLine($"=== Windows 语音诊断报告 ===");
            writer.WriteLine($"检测时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"系统中共有 {allVoices.Count} 个语音包");
            writer.WriteLine();

            int enabledCount = 0;
            int chineseCount = 0;

            foreach (var voice in allVoices)
            {
                var info = voice.VoiceInfo;

                // 统计
                if (voice.Enabled) enabledCount++;
                if (info.Culture.Name.StartsWith("zh")) chineseCount++;

                // 控制台输出
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"名称: {info.Name}");
                Console.WriteLine($"  描述: {info.Description}");
                Console.WriteLine($"  文化: {info.Culture.Name} ({info.Culture.NativeName})");
                Console.WriteLine($"  性别: {GetGenderName(info.Gender)}");
                Console.WriteLine($"  年龄: {GetAgeName(info.Age)}");
                Console.WriteLine($"  状态: {(voice.Enabled ? "✓ 已启用" : "✗ 已禁用")}");
                Console.WriteLine($"  ID: {info.Id}");

                // 文件输出
                writer.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                writer.WriteLine($"名称: {info.Name}");
                writer.WriteLine($"  描述: {info.Description}");
                writer.WriteLine($"  文化: {info.Culture.Name} ({info.Culture.NativeName})");
                writer.WriteLine($"  性别: {GetGenderName(info.Gender)}");
                writer.WriteLine($"  年龄: {GetAgeName(info.Age)}");
                writer.WriteLine($"  状态: {(voice.Enabled ? "✓ 已启用" : "✗ 已禁用")}");
                writer.WriteLine($"  ID: {info.Id}");
                writer.WriteLine();
            }

            Console.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"统计信息:");
            Console.WriteLine($"  总语音数: {allVoices.Count}");
            Console.WriteLine($"  已启用: {enabledCount}");
            Console.WriteLine($"  已禁用: {allVoices.Count - enabledCount}");
            Console.WriteLine($"  中文语音: {chineseCount}");
            Console.WriteLine($"\n诊断报告已保存到: {logFile}");

            // 测试语音播放
            if (enabledCount > 0)
            {
                Console.WriteLine("\n是否测试语音播放？(y/n)");
                var input = Console.ReadLine();
                if (input?.ToLower() == "y")
                {
                    TestVoices(allVoices);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 发生错误: {ex.Message}");
            Console.WriteLine($"详细信息: {ex}");
        }

        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }

    static void TestVoices(System.Collections.ObjectModel.ReadOnlyCollection<InstalledVoice> voices)
    {
        foreach (var voice in voices)
        {
            if (!voice.Enabled) continue;

            var info = voice.VoiceInfo;
            Console.WriteLine($"\n正在测试: {info.Name} ({info.Culture.Name})");
            Console.WriteLine("按 Enter 播放测试音频，按 S 跳过...");

            var key = Console.ReadLine();
            if (key?.ToLower() == "s") continue;

            try
            {
                using var synth = new SpeechSynthesizer();
                synth.SelectVoice(info.Name);
                synth.Speak($"你好，我是{info.Name}。Hello, this is {info.Name}.");
                Console.WriteLine("  ✓ 播放成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 播放失败: {ex.Message}");
            }
        }
    }

    static string GetGenderName(VoiceGender gender)
    {
        return gender switch
        {
            VoiceGender.Male => "男声",
            VoiceGender.Female => "女声",
            VoiceGender.Neutral => "中性",
            _ => "未知"
        };
    }

    static string GetAgeName(VoiceAge age)
    {
        return age switch
        {
            VoiceAge.Child => "儿童",
            VoiceAge.Teen => "少年",
            VoiceAge.Adult => "成人",
            VoiceAge.Senior => "老年",
            _ => "未知"
        };
    }
}
