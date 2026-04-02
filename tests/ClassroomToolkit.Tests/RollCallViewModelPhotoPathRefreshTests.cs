using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Models;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallViewModelPhotoPathRefreshTests
{
    [Fact]
    public void TryRollNext_ShouldRefreshCurrentStudentPhotoPath_ForRolledStudent()
    {
        var className = $"班级_{Guid.NewGuid():N}";
        var studentId = $"S{Guid.NewGuid():N}";
        var studentName = "张三";

        var photoRoot = StudentResourceLocator.ResolveStudentPhotoRoot();
        var classDirectory = Path.Combine(photoRoot, className);
        Directory.CreateDirectory(classDirectory);
        var expectedPhotoPath = Path.Combine(classDirectory, $"{studentId}.jpg");
        File.WriteAllBytes(expectedPhotoPath, new byte[] { 0x01, 0x02, 0x03 });

        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase)
            {
                [className] = new ClassRoster(
                    className,
                    new[] { StudentRecord.Create(studentId, studentName, className, "一组") })
            },
            className);
        var useCase = new RollCallWorkbookUseCase(new StubStore(new RollCallWorkbookStoreLoadData(
            workbook,
            CreatedTemplate: false,
            RollStateJson: null)));

        using var viewModel = new RollCallViewModel("students.xlsx", useCase);
        try
        {
            viewModel.LoadData();
            viewModel.CurrentStudentPhotoPath.Should().BeNull();

            var rolled = viewModel.TryRollNext(out var message);

            rolled.Should().BeTrue();
            message.Should().BeNull();
            viewModel.CurrentStudentId.Should().Be(studentId);
            viewModel.CurrentStudentName.Should().Be(studentName);
            viewModel.CurrentStudentPhotoPath.Should().Be(expectedPhotoPath);
        }
        finally
        {
            if (File.Exists(expectedPhotoPath))
            {
                File.Delete(expectedPhotoPath);
            }

            if (Directory.Exists(classDirectory)
                && !Directory.EnumerateFileSystemEntries(classDirectory).Any())
            {
                Directory.Delete(classDirectory);
            }
        }
    }

    [Fact]
    public void SetCurrentStudentByIndex_ShouldClearPreviousPhotoPath_BeforeApplyingNextPhotoPath()
    {
        var className = $"班级_{Guid.NewGuid():N}";
        var studentAId = $"S{Guid.NewGuid():N}";
        var studentBId = $"S{Guid.NewGuid():N}";
        var studentAName = "张三";
        var studentBName = "李四";

        var photoRoot = StudentResourceLocator.ResolveStudentPhotoRoot();
        var classDirectory = Path.Combine(photoRoot, className);
        Directory.CreateDirectory(classDirectory);
        var photoAPath = Path.Combine(classDirectory, $"{studentAId}.jpg");
        var photoBPath = Path.Combine(classDirectory, $"{studentBId}.jpg");
        File.WriteAllBytes(photoAPath, new byte[] { 0x01, 0x02, 0x03 });
        File.WriteAllBytes(photoBPath, new byte[] { 0x04, 0x05, 0x06 });

        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster>(StringComparer.OrdinalIgnoreCase)
            {
                [className] = new ClassRoster(
                    className,
                    new[]
                    {
                        StudentRecord.Create(studentAId, studentAName, className, "一组"),
                        StudentRecord.Create(studentBId, studentBName, className, "一组")
                    })
            },
            className);
        var useCase = new RollCallWorkbookUseCase(new StubStore(new RollCallWorkbookStoreLoadData(
            workbook,
            CreatedTemplate: false,
            RollStateJson: null)));

        using var viewModel = new RollCallViewModel("students.xlsx", useCase);
        try
        {
            viewModel.LoadData();
            viewModel.SetCurrentStudentByIndex(0).Should().BeTrue();
            viewModel.CurrentStudentPhotoPath.Should().Be(photoAPath);

            var photoPathChanges = new List<string?>();
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(RollCallViewModel.CurrentStudentPhotoPath))
                {
                    photoPathChanges.Add(viewModel.CurrentStudentPhotoPath);
                }
            };

            viewModel.SetCurrentStudentByIndex(1).Should().BeTrue();
            viewModel.CurrentStudentPhotoPath.Should().Be(photoBPath);
            photoPathChanges.Should().ContainInOrder((string?)null, photoBPath);
        }
        finally
        {
            if (File.Exists(photoAPath))
            {
                File.Delete(photoAPath);
            }

            if (File.Exists(photoBPath))
            {
                File.Delete(photoBPath);
            }

            if (Directory.Exists(classDirectory)
                && !Directory.EnumerateFileSystemEntries(classDirectory).Any())
            {
                Directory.Delete(classDirectory);
            }
        }
    }

    private sealed class StubStore : IRollCallWorkbookStore
    {
        private readonly RollCallWorkbookStoreLoadData _loadData;

        public StubStore(RollCallWorkbookStoreLoadData loadData)
        {
            _loadData = loadData;
        }

        public RollCallWorkbookStoreLoadData LoadOrCreate(string path)
        {
            _ = path;
            return _loadData;
        }

        public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
        {
            _ = workbook;
            _ = path;
            _ = rollStateJson;
        }
    }
}
