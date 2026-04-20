using ClassroomToolkit.Domain.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class IdentityUtilsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("nan", "")]
    [InlineData("None", "")]
    [InlineData("Nat", "")]
    [InlineData(" 张三 ", "张三")]
    public void NormalizeText_ShouldNormalizeExpected(string? input, string expected)
    {
        var result = IdentityUtils.NormalizeText(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void CompactText_ShouldRemoveWhitespace()
    {
        var result = IdentityUtils.CompactText(" A  B\tC ");

        result.Should().Be("ABC");
    }

    [Fact]
    public void NormalizeGroupName_ShouldUppercase()
    {
        var result = IdentityUtils.NormalizeGroupName(" group-a ");

        result.Should().Be("GROUP-A");
    }

    [Fact]
    public void BuildRowKey_ShouldReturnEmpty_WhenAllPartsAreEmpty()
    {
        var result = IdentityUtils.BuildRowKey(" ", " ", " ", " ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildRowKey_ShouldBeDeterministic_ForEquivalentInputs()
    {
        var key1 = IdentityUtils.BuildRowKey(" 1001 ", "张三", "一班", "一组");
        var key2 = IdentityUtils.BuildRowKey("1001", "张三", "一班", "一组");

        key1.Should().NotBeEmpty();
        key1.Should().Be(key2);
    }

    [Fact]
    public void BuildRowKey_ShouldDocumentSha1CompatibilitySuppression()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Domain",
            "Utilities",
            "IdentityUtils.cs"));

        source.Should().Contain("CA5350:Do Not Use Weak Cryptographic Algorithms");
        source.Should().Contain("compatibility identifier");
    }
}
