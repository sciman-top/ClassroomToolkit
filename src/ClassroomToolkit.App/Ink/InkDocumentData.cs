using System.Collections.Generic;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Top-level container for persisted ink annotations associated with a source file.
/// Serialized as the root object in .ink.json sidecar files.
/// </summary>
public sealed class InkDocumentData
{
    public int Version { get; set; } = 1;
    public string SourcePath { get; set; } = string.Empty;
    public List<InkPageData> Pages { get; set; } = new();
}
