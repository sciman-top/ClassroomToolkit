using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallAndExportCopyContractTests
{
    [Fact]
    public void RollCallAndExportRuntimeCopy_ShouldStayCompact()
    {
        var rollCallState = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.State.cs"));
        var rollCallInput = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Input.cs"));
        var exportSource = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Export.cs"));

        rollCallState.Should().Contain("学生名册读取失败，请检查是否被占用或已损坏。");
        rollCallState.Should().Contain("学生名册保存失败，请先关闭 Excel。");
        rollCallState.Should().Contain("设置保存失败：");
        rollCallState.Should().Contain("请检查权限或磁盘状态。");
        rollCallState.Should().Contain("语音播报不可用，可能缺少系统语音包或组件。请安装中文语音包后重启。");

        rollCallInput.Should().Contain("翻页笔监听不可用，可能被拦截。可尝试以管理员身份运行。");
        rollCallInput.Should().Contain("确定要重置所有分组点名状态并重新开始吗？");

        exportSource.Should().Contain("检测到该文件有笔迹，是否加载？");
        exportSource.Should().Contain("截图临时文件不存在，无法导出。");
        exportSource.Should().Contain("当前目录没有可导出内容。");
        exportSource.Should().Contain("导出异常：");
        exportSource.Should().Contain("自动保存失败，已中止导出。");
        exportSource.Should().Contain("共 {outputs.Count} 个文件。");
        exportSource.Should().Contain("导出中 ({done}/{total}, 并发 {maxParallel})");
    }
}
