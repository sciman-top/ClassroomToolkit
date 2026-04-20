using ClassroomToolkit.Infra.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsRepositoryTests
{
    [Fact]
    public void Save_ShouldThrow_WhenDataIsNull()
    {
        var path = TestPathHelper.CreateFilePath("ctool_settings_null", ".ini");
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
        var path = TestPathHelper.CreateFilePath("ctool_settings", ".ini");
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
        var path = TestPathHelper.CreateFilePath("ctool_settings", ".ini");
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

    [Fact]
    public void Save_ShouldThrow_WhenExistingSettingsFileIsUnreadable_WithoutPriorLoad()
    {
        var path = TestPathHelper.CreateFilePath("ctool_settings_corrupt", ".ini");
        try
        {
            File.WriteAllBytes(path, new byte[] { 0x5B, 0x50, 0x00, 0x61, 0x69, 0x6E, 0x74, 0x5D });
            var original = File.ReadAllBytes(path);
            var repo = new SettingsRepository(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "10"
                }
            };

            Action act = () => repo.Save(data);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*已阻止写入*");
            File.ReadAllBytes(path).Should().Equal(original);
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
    public void Save_ShouldThrow_WhenValidatedSettingsFileBecomesUnreadable_BeforeSave()
    {
        var path = TestPathHelper.CreateFilePath("ctool_settings_corrupt_after_load", ".ini");
        try
        {
            File.WriteAllText(path, "[Paint]\nbrush_base_size=8\n");
            var repo = new SettingsRepository(path);
            _ = repo.Load();
            repo.LastLoadSucceeded.Should().BeTrue();

            File.WriteAllBytes(path, new byte[] { 0x5B, 0x50, 0x00, 0x61, 0x69, 0x6E, 0x74, 0x5D });
            var original = File.ReadAllBytes(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "11"
                }
            };

            Action act = () => repo.Save(data);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*已阻止写入*");
            File.ReadAllBytes(path).Should().Equal(original);
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
    public void Save_ShouldThrow_WhenValidatedSettingsFileChangesWithoutTimestampDrift()
    {
        var path = TestPathHelper.CreateFilePath("ctool_settings_corrupt_same_timestamp", ".ini");
        try
        {
            File.WriteAllText(path, "[Paint]\nbrush_base_size=8\n");
            var repo = new SettingsRepository(path);
            _ = repo.Load();
            repo.LastLoadSucceeded.Should().BeTrue();

            var validatedWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            File.WriteAllBytes(path, new byte[] { 0x5B, 0x50, 0x00, 0x61, 0x69, 0x6E, 0x74, 0x5D });
            File.SetLastWriteTimeUtc(path, validatedWriteTimeUtc);
            var original = File.ReadAllBytes(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brush_base_size"] = "12"
                }
            };

            Action act = () => repo.Save(data);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*已阻止写入*");
            File.ReadAllBytes(path).Should().Equal(original);
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
