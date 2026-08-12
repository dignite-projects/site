using Shouldly;
using Xunit;

namespace Dignite.Site;

public class IdentifierName_Tests
{
    [Theory]
    [InlineData("post-article")]
    [InlineData("seo")]
    [InlineData("blog")]
    [InlineData("my_field")]
    [InlineData("2026-report")]
    [InlineData("a")]
    public void Should_Accept_A_Well_Formed_Identifier(string value)
    {
        IdentifierName.IsValid(value).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not-Valid")]
    [InlineData("has space")]
    [InlineData("-leading-hyphen")]
    [InlineData("_leading-underscore")]
    [InlineData("has.dot")]
    [InlineData("我的字段")]
    public void Should_Reject_A_Malformed_Identifier(string value)
    {
        IdentifierName.IsValid(value).ShouldBeFalse();
    }
}
