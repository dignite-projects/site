using System;
using System.Threading.Tasks;
using Dignite.Site.Admin.Templates;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Templates;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Public.Templates;

public class SiteTemplatePublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteTemplatePublicAppService _publicAppService;
    private readonly ISiteTemplateAdminAppService _adminAppService;
    private readonly ICurrentTenant _currentTenant;

    public SiteTemplatePublicAppService_Tests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _publicAppService = GetRequiredService<ISiteTemplatePublicAppService>();
        _adminAppService = GetRequiredService<ISiteTemplateAdminAppService>();
    }

    [Fact]
    public async Task Should_Return_The_Published_Version_And_No_Layout_When_There_Is_None()
    {
        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);

        set.Template.ShouldNotBeNull();
        set.Template.Number.ShouldBe(2);
        set.Template.Body.ShouldBe("v2 {{ page.name }}");
        set.Layout.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Normalize_The_Requested_Name()
    {
        var set = await _publicAppService.GetRenderSetAsync("/" + SiteTestData.PublishedTemplateName);

        set.Template.ShouldNotBeNull();
        set.Template.Number.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Not_Return_A_Template_That_Was_Never_Published()
    {
        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.DraftTemplateName);

        set.Template.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Follow_The_Published_Pointer_When_A_Template_Is_Rolled_Back()
    {
        await _adminAppService.PublishAsync(SiteTestData.PublishedTemplateId, 1);

        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);

        set.Template.ShouldNotBeNull();
        set.Template.Number.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Carry_The_Published_Layout()
    {
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto
        {
            Name = SiteTemplateConsts.LayoutName,
            Body = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>"
        });

        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);

        set.Layout.ShouldNotBeNull();
        set.Layout.Body.ShouldContain("{{ site_head }}");
        set.Template.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Not_Carry_A_Layout_That_Is_Only_Staged()
    {
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto
        {
            Name = SiteTemplateConsts.LayoutName,
            Body = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>",
            Publish = false
        });

        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);

        set.Layout.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Return_Both_Null_For_An_Unknown_Name()
    {
        var set = await _publicAppService.GetRenderSetAsync("does/not-exist");

        set.Template.ShouldBeNull();
        set.Layout.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Carry_Published_Partials_With_Their_Bodies_And_Not_Staged_Ones()
    {
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = "partials/card", Body = "<b>{{ title }}</b>" });
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = "partials/staged", Body = "<i>x</i>", Publish = false });

        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);

        set.Partials.Keys.ShouldBe(new[] { "partials/card" });
        set.Partials["partials/card"].Body.ShouldBe("<b>{{ title }}</b>");
        set.Partials["partials/card"].Number.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Carry_The_Version_Id_Of_A_Published_Asset_But_Not_As_A_Partial()
    {
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = "assets/site.css", Body = "a{color:red}" });
        var asset = await _adminAppService.FindByNameAsync("assets/site.css");
        await _adminAppService.UpdateAsync(asset!.Id, new UpdateSiteTemplateDto { Body = "a{color:blue}" });

        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);

        // The id of the published version (the second), not its number: a number can repeat after a template is
        // deleted and created again, an id cannot.
        var published = await _publicAppService.FindPublishedAsync("assets/site.css");
        published.ShouldNotBeNull();
        published.Number.ShouldBe(2);
        set.Assets["assets/site.css"].ShouldBe(published.Id);
        set.Partials.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Not_Carry_An_Asset_That_Is_Only_Staged()
    {
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = "assets/draft.css", Body = "a{}", Publish = false });

        var set = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);

        set.Assets.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("partials/card")]
    [InlineData("assets/site.css")]
    public async Task Should_Not_Return_A_Published_Partial_Or_Asset_As_The_Template(string name)
    {
        // A page whose Template pointed at a fragment must fall through to its Razor view, not render the
        // fragment without site_head.
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = name, Body = "x" });

        var set = await _publicAppService.GetRenderSetAsync(name);

        set.Template.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Not_Carry_A_Partial_Published_Under_Another_Tenant()
    {
        var tenantId = Guid.NewGuid();

        using (_currentTenant.Change(tenantId))
        {
            await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = "partials/tenant-only", Body = "<p>t</p>" });

            var inTenant = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);
            inTenant.Partials.Keys.ShouldContain("partials/tenant-only");
        }

        var asHost = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);
        asHost.Partials.Keys.ShouldNotContain("partials/tenant-only");

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var other = await _publicAppService.GetRenderSetAsync(SiteTestData.PublishedTemplateName);
            other.Partials.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Should_Find_The_Published_Version_Of_Any_Template_By_Name()
    {
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = "assets/site.css", Body = "a{color:red}" });

        var found = await _publicAppService.FindPublishedAsync("assets/site.css");

        found.ShouldNotBeNull();
        found.Body.ShouldBe("a{color:red}");
        found.Number.ShouldBe(1);

        // Spelled with a leading slash, like a page's template value.
        (await _publicAppService.FindPublishedAsync("/assets/site.css")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Not_Find_A_Draft_Or_An_Unknown_Name()
    {
        await _adminAppService.CreateAsync(new CreateSiteTemplateDto { Name = "assets/draft.css", Body = "a{}", Publish = false });

        (await _publicAppService.FindPublishedAsync("assets/draft.css")).ShouldBeNull();
        (await _publicAppService.FindPublishedAsync(SiteTestData.DraftTemplateName)).ShouldBeNull();
        (await _publicAppService.FindPublishedAsync("assets/missing.css")).ShouldBeNull();
    }
}
