using ClassroomToolkit.Infra.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class IniSettingsStoreSaveTests
{
    [Fact]
    public void Save_ShouldNotLeaveTempFile_WhenTargetIsLocked()
    {
        var path = TestPathHelper.CreateFilePath("ctool_ini_save", ".ini");
        try
        {
            File.WriteAllText(path, "[Paint]\nbrush_base_size=8\n");
            using var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var store = new IniSettingsStore(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "9"
                }
            };

            Action act = () => store.Save(data);

            act.Should().Throw<IOException>();
            var directory = Path.GetDirectoryName(path) ?? TestPathHelper.CreateDirectory("ctool_ini_save_fallback");
            var tempFiles = Directory.GetFiles(directory, $"{Path.GetFileName(path)}.*.tmp");
            tempFiles.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
