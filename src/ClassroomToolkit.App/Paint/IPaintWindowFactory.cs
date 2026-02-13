namespace ClassroomToolkit.App.Paint;

public interface IPaintWindowFactory
{
    (PaintOverlayWindow overlay, PaintToolbarWindow toolbar) Create();
}
