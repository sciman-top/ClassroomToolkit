namespace ClassroomToolkit.App.Paint;

public sealed class PaintWindowFactory : IPaintWindowFactory
{
    public (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create()
    {
        return (new PaintOverlayWindow(), new PaintToolbarWindow());
    }
}
