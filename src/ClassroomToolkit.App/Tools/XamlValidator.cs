using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClassroomToolkit.App.Tools
{
    /// <summary>
    /// XAML 验证工具，检查 Border 控件的 BorderBrush 设置
    /// </summary>
    public class XamlValidator
    {
        private static readonly Regex BorderRegex = new Regex(
            @"<Border[^>]*CornerRadius[^>]*>(.*?)</Border>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );

        private static readonly Regex BorderBrushRegex = new Regex(
            @"BorderBrush\s*=\s*[""'][^""']*[""']",
            RegexOptions.IgnoreCase
        );

        public static List<string> ValidateBorderBrush(string xamlContent, string fileName)
        {
            var issues = new List<string>();
            var matches = BorderRegex.Matches(xamlContent);

            for (int i = 0; i < matches.Count; i++)
            {
                var borderTag = matches[i].Value;
                
                // 检查是否有 BorderBrush 设置
                if (!BorderBrushRegex.IsMatch(borderTag))
                {
                    // 提取行号（简化处理）
                    var lineNumber = xamlContent.Substring(0, matches[i].Index)
                        .Split('\n').Length;
                    
                    issues.Add($"{fileName}:{lineNumber} - Border with CornerRadius missing BorderBrush");
                }
            }

            return issues;
        }

        public static void ValidateAllXamlFiles(string directory)
        {
            var allIssues = new List<string>();

            foreach (var file in Directory.GetFiles(directory, "*.xaml", SearchOption.AllDirectories))
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var issues = ValidateBorderBrush(content, Path.GetFileName(file));
                    allIssues.AddRange(issues);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error validating {file}: {ex.Message}");
                }
            }

            if (allIssues.Any())
            {
                Console.WriteLine("发现 BorderBrush 问题:");
                foreach (var issue in allIssues)
                {
                    Console.WriteLine($"  ❌ {issue}");
                }
            }
            else
            {
                Console.WriteLine("✅ 所有 Border 控件都正确设置了 BorderBrush");
            }
        }
    }
}
