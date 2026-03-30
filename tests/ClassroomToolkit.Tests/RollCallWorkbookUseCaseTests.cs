using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Application.UseCases.RollCall;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Serialization;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallWorkbookUseCaseTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenStoreIsNull()
    {
        var act = () => _ = new RollCallWorkbookUseCase(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Load_ShouldDeserializeRollState()
    {
        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster>
            {
                ["班级1"] = new("班级1", new[] { StudentRecord.Create("101", "张三", "班级1", "一组") })
            },
            "班级1");

        var rollStateJson = RollStateSerializer.SerializeWorkbookStates(new Dictionary<string, ClassRollState>
        {
            ["班级1"] = new ClassRollState { CurrentGroup = "一组", CurrentStudent = "101" }
        });
        var store = new StubStore(new RollCallWorkbookStoreLoadData(workbook, false, rollStateJson));

        var useCase = new RollCallWorkbookUseCase(store);

        var result = useCase.Load("students.xlsx");

        result.ErrorMessage.Should().BeNull();
        result.Workbook.Should().BeSameAs(workbook);
        result.ClassStates.Should().ContainKey("班级1");
    }

    [Fact]
    public void Load_ShouldThrowArgumentException_WhenPathIsBlank()
    {
        var store = new StubStore();
        var useCase = new RollCallWorkbookUseCase(store);

        var act = () => _ = useCase.Load(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Load_ShouldReturnFallback_WhenStoreThrows()
    {
        var store = new StubStore(exception: new InvalidOperationException("boom"));
        var useCase = new RollCallWorkbookUseCase(store);

        var result = useCase.Load("students.xlsx");

        result.ErrorMessage.Should().Contain("学生名册读取失败");
        result.Workbook.ClassNames.Should().Contain("班级1");
        result.ClassStates.Should().BeEmpty();
    }

    [Fact]
    public void Load_ShouldRethrow_WhenStoreThrowsFatalException()
    {
        var store = new StubStore(exception: new AccessViolationException("fatal"));
        var useCase = new RollCallWorkbookUseCase(store);

        Action act = () => _ = useCase.Load("students.xlsx");

        act.Should().Throw<AccessViolationException>();
    }

    [Fact]
    public void Save_ShouldSerializeStates_AndDelegateToStore()
    {
        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster>
            {
                ["班级1"] = new("班级1", new[] { StudentRecord.Create("101", "张三", "班级1", "一组") })
            },
            "班级1");
        var store = new StubStore(new RollCallWorkbookStoreLoadData(workbook, false, null));
        var useCase = new RollCallWorkbookUseCase(store);

        var states = new Dictionary<string, ClassRollState>
        {
            ["班级1"] = new ClassRollState
            {
                CurrentGroup = "一组",
                CurrentStudent = "101",
                GroupRemaining = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["全部"] = new List<string> { "101" }
                }
            }
        };

        useCase.Save("students.xlsx", workbook, states);

        store.SavedPath.Should().Be("students.xlsx");
        var parsed = RollStateSerializer.DeserializeWorkbookStates(store.SavedRollStateJson!);
        parsed.Should().NotBeNull();
        parsed!.Should().ContainKey("班级1");
    }

    [Fact]
    public void Save_ShouldThrowArgumentException_WhenPathIsBlank()
    {
        var store = new StubStore(exception: null, loadData: new RollCallWorkbookStoreLoadData(
            new StudentWorkbook(
                new Dictionary<string, ClassRoster> { ["班级1"] = new("班级1", Array.Empty<StudentRecord>()) },
                "班级1"),
            false,
            null));
        var useCase = new RollCallWorkbookUseCase(store);
        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster> { ["班级1"] = new("班级1", Array.Empty<StudentRecord>()) },
            "班级1");

        var act = () => useCase.Save(" ", workbook, new Dictionary<string, ClassRollState>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_ShouldThrowArgumentNullException_WhenWorkbookIsNull()
    {
        var store = new StubStore();
        var useCase = new RollCallWorkbookUseCase(store);

        var act = () => useCase.Save("students.xlsx", null!, new Dictionary<string, ClassRollState>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Save_ShouldThrowArgumentNullException_WhenClassStatesIsNull()
    {
        var store = new StubStore();
        var useCase = new RollCallWorkbookUseCase(store);
        var workbook = new StudentWorkbook(
            new Dictionary<string, ClassRoster> { ["班级1"] = new("班级1", Array.Empty<StudentRecord>()) },
            "班级1");

        var act = () => useCase.Save("students.xlsx", workbook, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class StubStore : IRollCallWorkbookStore
    {
        private readonly RollCallWorkbookStoreLoadData? _loadData;
        private readonly Exception? _exception;

        public string? SavedPath { get; private set; }
        public string? SavedRollStateJson { get; private set; }

        public StubStore(RollCallWorkbookStoreLoadData? loadData = null, Exception? exception = null)
        {
            _loadData = loadData;
            _exception = exception;
        }

        public RollCallWorkbookStoreLoadData LoadOrCreate(string path)
        {
            if (_exception != null)
            {
                throw _exception;
            }

            return _loadData ?? throw new InvalidOperationException("No load data configured.");
        }

        public void Save(StudentWorkbook workbook, string path, string? rollStateJson)
        {
            SavedPath = path;
            SavedRollStateJson = rollStateJson;
        }
    }
}
