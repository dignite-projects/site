using Dignite.Site.Admin.Themes;
using Shouldly;
using Xunit;

namespace Dignite.Site.Application.Themes;

/// <summary>What a rendered page refers to by <c>#id</c> and does not define.</summary>
public class RenderedFragmentReferences_Tests
{
    [Fact]
    public void A_Use_Of_A_Symbol_That_Is_Not_In_The_Page_Should_Be_Found()
    {
        RenderedFragmentReferences.FindMissing("<svg><use href=\"#icon-github\"/></svg><symbol id=\"icon-x\"/>")
            .ShouldBe(new[] { "icon-github" });
    }

    [Fact]
    public void A_Reference_To_An_Id_The_Page_Has_Should_Not_Be_Found()
    {
        RenderedFragmentReferences.FindMissing("<symbol id='a'></symbol><use xlink:href=\"#a\"/><a href='#a'>x</a>")
            .ShouldBeEmpty();
    }

    [Fact]
    public void A_Url_Reference_Should_Be_Found_In_An_Attribute_And_In_Css()
    {
        RenderedFragmentReferences.FindMissing("<g fill=\"url(#grad)\"/><div style=\"clip-path: url( #clip )\"></div><clipPath id=\"clip\"/>")
            .ShouldBe(new[] { "grad" });
    }

    [Fact]
    public void Another_File_An_Empty_Fragment_And_Top_Should_Not_Be_Found()
    {
        RenderedFragmentReferences.FindMissing("<use href=\"sprite.svg#icon\"/><a href=\"#\">x</a><a href=\"#top\">up</a><a href=\"#TOP\">up</a>")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Each_Missing_Id_Should_Be_Reported_Once_In_The_Order_It_Is_First_Referred_To()
    {
        RenderedFragmentReferences.FindMissing("<a href=\"#b\"/><a href=\"#a\"/><a href=\"#b\"/>").ShouldBe(new[] { "b", "a" });
    }

    [Fact]
    public void An_Id_With_Encoded_Characters_Should_Match_Its_Definition()
    {
        RenderedFragmentReferences.FindMissing("<h2 id=\"caf&eacute;\"></h2><a href=\"#caf%C3%A9\">x</a>").ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_Should_Be_Found_In_Nothing()
    {
        RenderedFragmentReferences.FindMissing(null).ShouldBeEmpty();
        RenderedFragmentReferences.FindMissing("").ShouldBeEmpty();
    }
}
