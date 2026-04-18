using Xunit;

namespace ClassroomToolkit.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedWindowDragStateCollection
{
    public const string Name = "SharedWindowDragState";
}
