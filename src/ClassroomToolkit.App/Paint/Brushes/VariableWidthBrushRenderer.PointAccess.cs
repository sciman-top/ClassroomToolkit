using System.Collections.Generic;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint.Brushes;

internal partial class VariableWidthBrushRenderer
{
    public bool TryGetTipPosition(out WpfPoint tip)
    {
        if (_points.Count == 0)
        {
            tip = default;
            return false;
        }

        tip = _points[^1].Position;
        return true;
    }

    internal List<StrokePointData>? GetLastResampledStrokePointsForDiagnostics()
    {
        if (_points.Count < 2)
        {
            return null;
        }

        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2)
        {
            return null;
        }

        var result = new List<StrokePointData>(samples.Count);
        foreach (var sample in samples)
        {
            result.Add(new StrokePointData(sample.Position, sample.Width));
        }
        return result;
    }
}
