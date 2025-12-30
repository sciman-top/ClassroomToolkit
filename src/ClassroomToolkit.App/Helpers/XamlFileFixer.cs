using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace ClassroomToolkit.App.Helpers
{
    /// <summary>
    /// XAML 文件修复工具，在启动时修复所有 XAML 文件中的 BorderBrush 问题
    /// </summary>
    public static class XamlFileFixer
    {
        private static readonly Regex BorderRegex = new Regex(
            @"<Border[^>]*CornerRadius[^>]*>(.*?)</Border>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline
        );

        private static readonly Regex BorderBrushRegex = new Regex(
            @"BorderBrush\s*=\s*[""'][^""']*[""']",
            RegexOptions.IgnoreCase
        );

        public static void FixAllXamlFiles()
        {
            try
            {
                // 在运行时，我们无法修改源文件，所以这个方法暂时禁用
                // 实际的修复应该通过 BorderFixHelper 在运行时完成
                System.Diagnostics.Debug.WriteLine("XamlFileFixer: 运行时修复已禁用，使用 BorderFixHelper 进行运行时修复");
                
                // 如果需要修复源文件，请在开发时手动运行修复工具
                // 或者通过预构建脚本来完成
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"XamlFileFixer 错误: {ex.Message}");
            }
        }

        private static bool FixXamlFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var originalContent = content;
                var modified = false;

                var matches = BorderRegex.Matches(content);
                foreach (Match match in matches)
                {
                    var borderTag = match.Value;
                    
                    // 检查是否已经有 BorderBrush
                    if (!BorderBrushRegex.IsMatch(borderTag))
                    {
                        // 添加 BorderBrush="Transparent"
                        var fixedTag = AddBorderBrushToBorder(borderTag);
                        content = content.Replace(borderTag, fixedTag);
                        modified = true;
                        
                        System.Diagnostics.Debug.WriteLine($"XamlFileFixer: 修复 {Path.GetFileName(filePath)} 中的 Border");
                    }
                }

                if (modified)
                {
                    File.WriteAllText(filePath, content);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"XamlFileFixer 修复文件 {filePath} 失败: {ex.Message}");
            }

            return false;
        }

        private static string AddBorderBrushToBorder(string borderTag)
        {
            // 在 Border 标签中添加 BorderBrush="Transparent"
            // 寻找 CornerRadius 属性的位置
            var cornerRadiusMatch = Regex.Match(borderTag, @"CornerRadius\s*=\s*[^>]*");
            if (cornerRadiusMatch.Success)
            {
                var insertPosition = cornerRadiusMatch.Index + cornerRadiusMatch.Length;
                var before = borderTag.Substring(0, insertPosition);
                var after = borderTag.Substring(insertPosition);
                return $"{before} BorderBrush=\"Transparent\"{after}";
            }
            
            // 如果找不到 CornerRadius，在开始标签后添加
            var tagEndMatch = Regex.Match(borderTag, @"<Border[^>]*>");
            if (tagEndMatch.Success)
            {
                var insertPosition = tagEndMatch.Index + tagEndMatch.Length;
                var before = borderTag.Substring(0, insertPosition);
                var after = borderTag.Substring(insertPosition);
                return $"{before} BorderBrush=\"Transparent\"{after}";
            }

            return borderTag;
        }
    }
}
