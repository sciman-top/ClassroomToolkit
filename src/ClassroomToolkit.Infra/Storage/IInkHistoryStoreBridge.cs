using System;

namespace ClassroomToolkit.Infra.Storage;

public interface IInkHistoryStoreBridge
{
    InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex);
    void Save(string sourcePath, int pageIndex, string? strokesJson);
}
