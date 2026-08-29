using System.Security.Cryptography;
using ClassroomToolkit.Infra.Migration;
using ClassroomToolkit.Infra.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsRepositoryTests
{
    [Fact]
    public void Save_ShouldRefuseMigration_WhenBackupPathContainsDifferentContent()
    {
        var path = TestPathHelper.CreateFilePath("ctool_settings_migration_collision", ".ini");
        const string original = "[Paint]\nwps_input_mode=manual\nwps_raw_input=True\n";
        File.WriteAllText(path, original);
        string contentHash;
        using (var source = File.OpenRead(path))
        {
            contentHash = Convert.ToHexString(SHA256.HashData(source));
        }
        var backupPath = Path.Combine(
            Path.GetDirectoryName(path)!,
            $"{Path.GetFileNameWithoutExtension(path)}.bak-v2.0-{contentHash}.ini");
        File.WriteAllText(backupPath, "different content");

        try
        {
            var repo = new SettingsRepository(path);
            var data = repo.Load();

            var act = () => repo.Save(data);

            act.Should().Throw<IOException>();
            File.ReadAllText(path).Should().Be(original);
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_ShouldCreateOneDeduplicatedBackup_WhenPersistingMigration()
    {
        var path = TestPathHelper.CreateFilePath("ctool_settings_migration_backup", ".ini");
        var directory = Path.GetDirectoryName(path)!;
        var backupPattern = $"{Path.GetFileNameWithoutExtension(path)}.bak-*";
        const string original = "[Paint]\nwps_input_mode=manual\nwps_raw_input=True\n";
        File.WriteAllText(path, original);

        try
        {
            var repo = new SettingsRepository(path);
            var data = repo.Load();

            Directory.GetFiles(directory, backupPattern, SearchOption.TopDirectoryOnly)
                .Should().BeEmpty("loading settings must not create filesystem side effects");

            repo.Save(data);
            repo.Save(data);

            var backups = Directory.GetFiles(directory, backupPattern, SearchOption.TopDirectoryOnly);
            backups.Should().ContainSingle();
            File.ReadAllText(backups[0]).Should().Be(original);
        }
        finally
        {
            foreach (var backup in Directory.GetFiles(directory, backupPattern, SearchOption.TopDirectoryOnly))
            {
                File.Delete(backup);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

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

    [Fact]
    public void Save_ShouldNotMutateCallerData_WhenDataAlreadyContainsMetaSection()
    {
        var path = TestPathHelper.CreateFilePath("ctool_settings_meta_no_mutation", ".ini");
        try
        {
            var repo = new SettingsRepository(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["General"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["theme"] = "dark"
                },
                [SettingsMigrator.MetaSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["custom_marker"] = "keep-me"
                }
            };
            // 快照保存前的逐项内容，用于断言原对象完全未被突变。
            var snapshot = data.ToDictionary(
                pair => pair.Key,
                pair => pair.Value == null
                    ? null
                    : (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(pair.Value),
                StringComparer.OrdinalIgnoreCase);

            repo.Save(data);

            // 调用方对象逐项保持不变：_meta 不被写入版本号，section 引用内容不变。
            data.Should().HaveSameCount(snapshot);
            foreach (var pair in snapshot)
            {
                if (pair.Value == null)
                {
                    data[pair.Key].Should().BeNull();
                    continue;
                }

                data[pair.Key].Should().NotBeNull();
                data[pair.Key]!.Should().Equal(pair.Value!);
            }

            // 磁盘上的 _meta 才包含版本号。
            var reloaded = new SettingsRepository(path).Load();
            reloaded[SettingsMigrator.MetaSection][SettingsMigrator.VersionKey].Should().Be(SettingsMigrator.CurrentVersion);
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
    public void Save_ShouldNotMutateCallerData_WhenPersistenceFails()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_settings_meta_fail");
        // 目标路径是一个目录：EnsureExistingFileStateValidated 视为“文件不存在”放行，
        // 写盘阶段 File.Move 到目录上抛 IOException，构造“元数据注入之后才失败”的分支。
        var path = Path.Combine(directory, "settings.ini");
        Directory.CreateDirectory(path);

        try
        {
            var repo = new SettingsRepository(path);
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["General"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["theme"] = "dark"
                },
                [SettingsMigrator.MetaSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["custom_marker"] = "keep-me"
                }
            };

            var act = () => repo.Save(data);

            act.Should().Throw<Exception>();
            data["General"].Should().HaveCount(1);
            data["General"]["theme"].Should().Be("dark");
            data[SettingsMigrator.MetaSection].Should().HaveCount(1);
            data[SettingsMigrator.MetaSection]["custom_marker"].Should().Be("keep-me");
            data[SettingsMigrator.MetaSection].Should().NotContainKey(SettingsMigrator.VersionKey);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
