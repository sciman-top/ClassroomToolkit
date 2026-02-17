using ClassroomToolkit.Infra.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsRepositoryTests
{
    [Fact]
    public void Save_ShouldThrow_WhenDataIsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_settings_null_{Guid.NewGuid():N}.ini");
        try
        {
            var repo = new SettingsRepository(path);

            Action act = () => repo.Save(null!);

            act.Should().Throw<ArgumentNullException>();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_ShouldThrow_WhenLastLoadFailedAndSettingsFileExists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_settings_{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, "[Paint]\nbrush_base_size=8\n");
            using var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var repo = new SettingsRepository(path);
            _ = repo.Load();

            repo.LastLoadSucceeded.Should().BeFalse();
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "9"
                }
            };

            Action act = () => repo.Save(data);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*已阻止写入*");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_ShouldSucceed_WhenLastLoadSucceeded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_settings_{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, "[Paint]\nbrush_base_size=8\n");
            var repo = new SettingsRepository(path);
            _ = repo.Load();

            repo.LastLoadSucceeded.Should().BeTrue();
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "9"
                }
            };

            repo.Save(data);

            var content = File.ReadAllText(path);
            content.Should().Contain("brush_base_size=9");
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
