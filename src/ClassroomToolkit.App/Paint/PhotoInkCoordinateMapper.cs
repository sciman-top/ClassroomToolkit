using System;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkCoordinateMapper
{
    internal static Matrix CreateForwardMatrix(
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var matrix = Matrix.Identity;
        matrix.Scale(pageScaleX * photoScaleX, pageScaleY * photoScaleY);
        matrix.Translate(translateX, translateY);
        return matrix;
    }

    internal static Matrix CreateInverseMatrix(
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY,
        double epsilon = PhotoTransformMathDefaults.InverseScaleEpsilon)
    {
        return TryCreateInverseMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY,
            out var matrix,
            epsilon)
            ? matrix
            : Matrix.Identity;
    }

    internal static bool TryCreateInverseMatrix(
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY,
        out Matrix matrix,
        double epsilon = PhotoTransformMathDefaults.InverseScaleEpsilon)
    {
        var scaleX = pageScaleX * photoScaleX;
        var scaleY = pageScaleY * photoScaleY;
        if (Math.Abs(scaleX) < epsilon || Math.Abs(scaleY) < epsilon)
        {
            matrix = Matrix.Identity;
            return false;
        }

        matrix = Matrix.Identity;
        matrix.Scale(1.0 / scaleX, 1.0 / scaleY);
        matrix.Translate(-translateX / scaleX, -translateY / scaleY);
        return true;
    }

    internal static WpfPoint ToPhotoSpace(
        WpfPoint point,
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var inverse = CreateInverseMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY);
        return inverse.Transform(point);
    }

    internal static Geometry ToPhotoGeometry(
        Geometry geometry,
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var inverse = CreateInverseMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY);
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(inverse);
        var flattened = clone.GetFlattenedPathGeometry();
        if (flattened.CanFreeze)
        {
            flattened.Freeze();
        }

        return flattened;
    }

    internal static Geometry ToScreenGeometry(
        Geometry geometry,
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var transform = CreateForwardMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY);
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(transform);
        if (clone.CanFreeze)
        {
            clone.Freeze();
        }

        return clone;
    }
}
