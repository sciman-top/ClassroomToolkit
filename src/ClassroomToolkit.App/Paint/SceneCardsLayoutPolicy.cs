namespace ClassroomToolkit.App.Paint;

internal enum SceneCardsLayoutMode
{
    TwoColumns = 0,
    SingleColumn = 1
}

internal static class SceneCardsLayoutPolicy
{
    internal const double SingleColumnThreshold = 860;

    internal static SceneCardsLayoutMode Resolve(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth))
        {
            return SceneCardsLayoutMode.TwoColumns;
        }

        return availableWidth < SingleColumnThreshold
            ? SceneCardsLayoutMode.SingleColumn
            : SceneCardsLayoutMode.TwoColumns;
    }
}
