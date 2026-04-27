using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Top-level container for persisted ink annotations associated with a source file.
/// Serialized as the root object in .ink.json sidecar files.
/// </summary>
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List property is part of the persisted ink JSON contract.")]
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter is required for JSON deserialization compatibility.")]
public sealed class InkDocumentData
{
    public int Version { get; set; } = 1;
    public string SourcePath { get; set; } = string.Empty;
    public List<InkPageData> Pages { get; set; } = new();
}
