using System.Threading.Tasks;
using Dignite.Site.Admin.Templates;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Templates;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Templates;

public class SiteTemplatePublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteTemplatePublicAppService _publicAppService;
    private readonly ISiteTemplateAdminAppService _adminAppService;

    public SiteTemplatePublicAppService_Tests()
    {
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
}
