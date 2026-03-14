namespace ClassroomToolkit.App.Paint;

internal readonly record struct PhotoEnterTransformInitPlan(
    bool ShouldApplyUnifiedTransform,
    bool ShouldMarkUserDirtyAfterUnifiedApply,
    bool ShouldMarkUnifiedTransformReady,
    bool ShouldTryStoredTransform,
    bool ShouldResetIdentity);

internal static class PhotoEnterTransformInitPolicy
{
    internal static PhotoEnterTransformInitPlan Resolve(
        bool crossPageDisplayEnabled,
        bool rememberPhotoTransform,
        bool photoUnifiedTransformReady,
        bool hadUserTransformDirty)
    {
        if (!rememberPhotoTransform)
        {
            return new PhotoEnterTransformInitPlan(
                ShouldApplyUnifiedTransform: false,
                ShouldMarkUserDirtyAfterUnifiedApply: false,
                ShouldMarkUnifiedTransformReady: false,
                ShouldTryStoredTransform: false,
                ShouldResetIdentity: true);
        }

        if (crossPageDisplayEnabled)
        {
            if (photoUnifiedTransformReady)
            {
                return new PhotoEnterTransformInitPlan(
                    ShouldApplyUnifiedTransform: true,
                    ShouldMarkUserDirtyAfterUnifiedApply: true,
                    ShouldMarkUnifiedTransformReady: false,
                    ShouldTryStoredTransform: false,
                    ShouldResetIdentity: false);
            }

            if (hadUserTransformDirty)
            {
                return new PhotoEnterTransformInitPlan(
                    ShouldApplyUnifiedTransform: true,
                    ShouldMarkUserDirtyAfterUnifiedApply: false,
                    ShouldMarkUnifiedTransformReady: true,
                    ShouldTryStoredTransform: false,
                    ShouldResetIdentity: false);
            }

            return new PhotoEnterTransformInitPlan(
                ShouldApplyUnifiedTransform: false,
                ShouldMarkUserDirtyAfterUnifiedApply: false,
                ShouldMarkUnifiedTransformReady: false,
                ShouldTryStoredTransform: false,
                ShouldResetIdentity: true);
        }

        return new PhotoEnterTransformInitPlan(
            ShouldApplyUnifiedTransform: false,
            ShouldMarkUserDirtyAfterUnifiedApply: false,
            ShouldMarkUnifiedTransformReady: false,
            ShouldTryStoredTransform: true,
            ShouldResetIdentity: false);
    }
}
