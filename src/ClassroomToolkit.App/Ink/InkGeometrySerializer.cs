using System.Globalization;
using System.Windows.Media;

namespace ClassroomToolkit.App.Ink;

public static class InkGeometrySerializer
{
    public static string Serialize(Geometry geometry)
    {
        if (geometry == null)
        {
            return string.Empty;
        }
        var flattened = geometry.GetFlattenedPathGeometry();
        return flattened.ToString(CultureInfo.InvariantCulture);
    }

    public static Geometry? Deserialize(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }
        try
        {
            return Geometry.Parse(data);
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            return null;
        }
    }
}

