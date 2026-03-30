using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoEnterTransformInitPolicyTests
{
    [Fact]
    public void Resolve_ShouldApplyUnifiedAndMarkDirty_WhenCrossPageAndUnifiedReady()
    {
        var plan = PhotoEnterTransformInitPolicy.Resolve(
            crossPageDisplayEnabled: true,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: true,
            hadUserTransformDirty: false);

        plan.ShouldApplyUnifiedTransform.Should().BeTrue();
        plan.ShouldMarkUserDirtyAfterUnifiedApply.Should().BeTrue();
        plan.ShouldMarkUnifiedTransformReady.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeFalse();
        plan.ShouldResetIdentity.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldResetIdentity_WhenCrossPageAndMemoryDisabled()
    {
        var plan = PhotoEnterTransformInitPolicy.Resolve(
            crossPageDisplayEnabled: true,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: false,
            hadUserTransformDirty: true);

        plan.ShouldApplyUnifiedTransform.Should().BeFalse();
        plan.ShouldMarkUserDirtyAfterUnifiedApply.Should().BeFalse();
        plan.ShouldMarkUnifiedTransformReady.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeFalse();
        plan.ShouldResetIdentity.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldTryStoredTransform_WhenSinglePageAndRememberEnabled()
    {
        var plan = PhotoEnterTransformInitPolicy.Resolve(
            crossPageDisplayEnabled: false,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: false,
            hadUserTransformDirty: false);

        plan.ShouldApplyUnifiedTransform.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeTrue();
        plan.ShouldResetIdentity.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldResetIdentity_WhenNoOtherPlanMatches()
    {
        var plan = PhotoEnterTransformInitPolicy.Resolve(
            crossPageDisplayEnabled: false,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: false,
            hadUserTransformDirty: false);

        plan.ShouldApplyUnifiedTransform.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeFalse();
        plan.ShouldResetIdentity.Should().BeTrue();
    }
}
