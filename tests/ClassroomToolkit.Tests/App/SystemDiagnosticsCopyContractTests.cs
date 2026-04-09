using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class SystemDiagnosticsCopyContractTests
{
    [Fact]
    public void SystemDiagnostics_ShouldUseCompactVisibleCopy()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "SystemDiagnostics.cs"));
        var resultSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "DiagnosticsResult.cs"));
        var dialogSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "DiagnosticsDialog.xaml.cs"));

        source.Should().Contain("请放到可写目录，或以管理员身份运行。");
        source.Should().Contain("学生数据文件：不存在（首次会生成模板）");
        source.Should().Contain("照片目录：不存在（首次会创建）");
        source.Should().Contain("检测到多个目录：学生数据可能来自非当前目录。");
        source.Should().Contain("请确认目录一致，必要时清理多余副本。");
        source.Should().Contain("语音播报可能不可用：语音包异常。");
        source.Should().Contain("请在 Windows 10/11 上运行。");
        source.Should().Contain("在 Windows“语音/讲述人/语言包”里安装中文语音后重启。");
        source.Should().Contain("请确认当前目录可写。");
        resultSource.Should().Contain("检测到潜在兼容问题。");
        dialogSource.Should().Contain("当前窗口未接入设置服务，无法重置启动提示。");
        dialogSource.Should().Contain("已重新启用启动兼容性提示。下次启动会再次检测。");
    }
}
