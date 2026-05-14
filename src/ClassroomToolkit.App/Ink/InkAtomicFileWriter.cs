using System.Diagnostics;
using System.IO;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.App.Ink;

internal static class InkAtomicFileWriter
{
    internal static void WriteAllText(
        string path,
        string content,
        string diagnosticPrefix)
    {
        AtomicFileReplaceUtility.WriteAtomically(
            path,
            tempPath => File.WriteAllText(tempPath, content),
            onTempCleanupFailure: (tempPath, ex) =>
            {
                if (!AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    return;
                }

                Debug.WriteLine($"{diagnosticPrefix} temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });
    }
}
