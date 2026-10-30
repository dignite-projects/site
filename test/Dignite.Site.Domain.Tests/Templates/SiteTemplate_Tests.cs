using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.Templates;

public class SiteTemplate_Tests
{
    /// <summary>
    /// A template is found by the value Page.Template holds, and Page.SetTemplate trims and drops a leading
    /// slash - so the same normalization has to happen here, or the two would never compare equal.
    /// </summary>
    [Theory]
    [InlineData("/Blog/Post", "Blog/Post")]
    [InlineData("Blog/Post", "Blog/Post")]
    [InlineData("  /Blog/Post  ", "Blog/Post")]
    [InlineData("layout", "layout")]
    public void Should_Normalize_Name(string input, string expected)
    {
        SiteTemplate.NormalizeName(input).ShouldBe(expected);
        new SiteTemplate(Guid.NewGuid(), input).Name.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("../etc")]
    [InlineData("with space")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("/")]
    public void Should_Reject_An_Invalid_Name(string name)
    {
        Should.Throw<InvalidValueFormatException>(() => new SiteTemplate(Guid.NewGuid(), name));
    }

    [Fact]
    public void Should_Reject_A_Name_Longer_Than_A_Page_Template_Can_Be()
    {
        var tooLong = new string('a', SiteTemplateConsts.MaxNameLength + 1);

        Should.Throw<InvalidValueFormatException>(() => new SiteTemplate(Guid.NewGuid(), tooLong));
    }

    [Fact]
    public void Should_Number_Versions_From_One()
    {
        var template = new SiteTemplate(Guid.NewGuid(), "Blog/Post");

        template.AddVersion(Guid.NewGuid(), "one", null).Number.ShouldBe(1);
        template.AddVersion(Guid.NewGuid(), "two", "second").Number.ShouldBe(2);
        template.AddVersion(Guid.NewGuid(), "three", null).Number.ShouldBe(3);

        template.Versions.Count.ShouldBe(3);
        template.FindVersion(2)!.Comment.ShouldBe("second");
        template.GetLatestVersion()!.Number.ShouldBe(3);
    }

    [Fact]
    public void Should_Carry_The_Templates_Id_And_Tenant_Onto_A_Version()
    {
        var tenantId = Guid.NewGuid();
        var template = new SiteTemplate(Guid.NewGuid(), "Default", tenantId);

        var version = template.AddVersion(Guid.NewGuid(), "body", null);

        version.TemplateId.ShouldBe(template.Id);
        version.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Should_Allow_An_Empty_Body_But_Not_A_Null_One()
    {
        var template = new SiteTemplate(Guid.NewGuid(), "Default");

        template.AddVersion(Guid.NewGuid(), string.Empty, null).Body.ShouldBe(string.Empty);
        Should.Throw<ArgumentException>(() => template.AddVersion(Guid.NewGuid(), null!, null));
    }

    [Fact]
    public void Should_Reject_A_Body_Or_Comment_Over_Its_Limit()
    {
        var template = new SiteTemplate(Guid.NewGuid(), "Default");

        Should.Throw<ArgumentException>(() =>
            template.AddVersion(Guid.NewGuid(), new string('x', SiteTemplateConsts.MaxBodyLength + 1), null));
        Should.Throw<ArgumentException>(() =>
            template.AddVersion(Guid.NewGuid(), "body", new string('x', SiteTemplateConsts.MaxCommentLength + 1)));
    }

    [Fact]
    public void Should_Not_Be_Published_Until_A_Version_Is()
    {
        var template = new SiteTemplate(Guid.NewGuid(), "Default");
        template.AddVersion(Guid.NewGuid(), "one", null);
        template.AddVersion(Guid.NewGuid(), "two", null);

        template.PublishedVersionNumber.ShouldBeNull();

        template.Publish(2);
        template.PublishedVersionNumber.ShouldBe(2);

        // Rolling back is publishing an earlier version.
        template.Publish(1);
        template.PublishedVersionNumber.ShouldBe(1);
    }

    [Fact]
    public void Should_Reject_Publishing_A_Version_That_Does_Not_Exist()
    {
        var template = new SiteTemplate(Guid.NewGuid(), "Default");
        template.AddVersion(Guid.NewGuid(), "one", null);

        Should.Throw<SiteTemplateVersionNotFoundException>(() => template.Publish(9));
        template.PublishedVersionNumber.ShouldBeNull();
    }

    [Fact]
    public void Should_Have_No_Latest_Version_When_Empty()
    {
        var template = new SiteTemplate(Guid.NewGuid(), "Default");

        template.GetLatestVersion().ShouldBeNull();
        template.FindVersion(1).ShouldBeNull();
    }
}
