using System.Reflection;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Models;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallViewModelPreloadConcurrencyTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
    private static readonly FieldInfo LoadOrchestratorField = typeof(RollCallViewModel)
        .GetField("_loadOrchestrator", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo PreloadTaskField = typeof(RollCallWorkbookLoadOrchestrator)
        .GetField("_preloadTask", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void WarmupData_ShouldNotClearLatestPreloadTask_WhenOlderTaskCompletes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ctoolkit-preload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var path1 = Path.Combine(tempRoot, "students-a.xlsx");
        var path2 = Path.Combine(tempRoot, "students-b.xlsx");
        File.WriteAllText(path1, "a");
        File.WriteAllText(path2, "b");

        var store = new CoordinatedStore(path1, path2);
        var useCase = new RollCallWorkbookUseCase(store);
        var viewModel = new RollCallViewModel(path1, useCase);

        try
        {
            viewModel.WarmupData(path1);
            viewModel.WarmupData(path2);

            store.Path1Started.Wait(WaitTimeout).Should().BeTrue();
            store.Path2Started.Wait(WaitTimeout).Should().BeTrue();

            store.Path1Release.Set();
            store.Path1Completed.Wait(WaitTimeout).Should().BeTrue();

            var staleOverwriteObserved = SpinWait.SpinUntil(
                () => GetPreloadTask(viewModel) is null,
                TimeSpan.FromSeconds(1));

            staleOverwriteObserved.Should().BeFalse("older preload completion must not clear newer preload task reference");

            var latestTask = GetPreloadTask(viewModel);
            latestTask.Should().NotBeNull();
            latestTask!.IsCompleted.Should().BeFalse();
        }
        finally
        {
            store.Path2Release.Set();
            viewModel.Dispose();
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static Task? GetPreloadTask(RollCallViewModel viewModel)
    {
        var orchestrator = LoadOrchestratorField.GetValue(viewModel);
        return PreloadTaskField.GetValue(orchestrator) as Task;
    }

    private sealed class CoordinatedStore : IRollCallWorkbookStore
    {
        private readonly string _path1;
        private readonly string _path2;

        public CoordinatedStore(string path1, string path2)
        {
            _path1 = path1;
            _path2 = path2;
        }

        public ManualResetEventSlim Path1Started { get; } = new(false);
        public ManualResetEventSlim Path1Release { get; } = new(false);
        public ManualResetEventSlim Path1Completed { get; } = new(false);
        public ManualResetEventSlim Path2Started { get; } = new(false);
        public ManualResetEventSlim Path2Release { get; } = new(false);

        public RollCallWorkbookStoreLoadData LoadOrCreate(string path)
        {
            if (string.Equals(path, _path1, StringComparison.OrdinalIgnoreCase))
            {
                Path1Started.Set();
                if (!Path1Release.Wait(WaitTimeout))
                {
                    throw new TimeoutException("path1 preload release timed out.");
                }

                Path1Completed.Set();
                return BuildLoadData("班级A");
            }

            if (string.Equals(path, _path2, StringComparison.OrdinalIgnoreCase))
            {
                Path2Started.Set();
                if (!Path2Release.Wait(WaitTimeout))
                {
                    throw new TimeoutException("path2 preload release timed out.");
                }

                return BuildLoadData("班级B");
            }

            throw new InvalidOperationException($"Unexpected load path: {path}");
        }

        public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
        {
        }

        private static RollCallWorkbookStoreLoadData BuildLoadData(string className)
        {
            var student = StudentRecord.Create("001", "张三", className, "一组");
            var roster = new ClassRoster(className, new[] { student });
            var workbook = new StudentWorkbook(
                new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase)
                {
                    [className] = roster
                },
                className);
            return new RollCallWorkbookStoreLoadData(workbook, CreatedTemplate: false, RollStateJson: null);
        }
    }
}
